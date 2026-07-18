using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class BuyExpiryHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static CallbackQuery DeserializeCallbackQuery(string json) =>
        JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        botMock.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return botMock;
    }

    [Fact]
    public async Task BuyStepHandler_Step1_NormalMode_AdvancesToStep2()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState
        {
            Step = 1,
            GroupId = 10,
            AddedByName = "Alice",
        });

        var pendingAddService = new PendingAddService();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyStepHandler(bot.Object, dialogService, pendingAddService, localizer.Object);
        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"Молоко\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state.Step);
        Assert.Equal("Молоко", state.Name);
    }

    [Fact]
    public async Task BuyStepHandler_Step2_SendsReviewMessage()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState
        {
            Step = 2,
            GroupId = 10,
            Name = "Молоко",
            AddedByName = "Alice",
        });

        var pendingAddService = new PendingAddService();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyStepHandler(bot.Object, dialogService, pendingAddService, localizer.Object);
        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"2л\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        // Dialog cleared after step 2 — item is now pending
        Assert.Null(dialogService.GetState(-100L, 42L));
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuyStepHandler_UnknownStep_IsNoOp()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        var state = new BuyDialogState
        {
            Step = 3,
            GroupId = 10,
            Name = "Молоко",
            Quantity = "2л",
            AddedByName = "Alice",
        };
        dialogService.SetState(-100L, 42L, state);

        var pendingAddService = new PendingAddService();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(-100L, "buy.invalid-date")).Returns("Invalid format");

        var handler = new BuyStepHandler(bot.Object, dialogService, pendingAddService, localizer.Object);
        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"invalid\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        // No state change for unknown step
        var updatedState = dialogService.GetState(-100L, 42L);
        Assert.NotNull(updatedState);
        Assert.Equal(3, updatedState.Step);
    }

    [Fact]
    public async Task BuySkipCallbackHandler_DirectlySavesItem()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState
        {
            Step = 2,
            GroupId = 10,
            Name = "Молоко",
            AddedByName = "Alice",
        });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(10, "Молоко", null, "Alice", null))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Молоко", AddedByName = "Alice" });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var tagRepo = new Mock<TagRepository>("Data Source=file::memory:");
        tagRepo.Setup(r => r.GetTopTagsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());
        var tagCaptureServiceMock = new Mock<TagCaptureService>(
            bot.Object, new PendingDialogService<TagCaptureDialogState>(), tagRepo.Object, localizer.Object);
        tagCaptureServiceMock.Setup(s => s.StartTagCaptureAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new BuySkipCallbackHandler(
            bot.Object,
            dialogService,
            itemRepo.Object,
            Mock.Of<IHistoryRepository>(),
            tagCaptureServiceMock.Object,
            localizer.Object,
            Mock.Of<ILogger<BuySkipCallbackHandler>>());

        var callback = DeserializeCallbackQuery("{\"id\":\"cb1\",\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat_instance\":\"123\",\"message\":{\"message_id\":2,\"chat\":{\"id\":-100}},\"data\":\"buy:skip_quantity\"}");
        await handler.HandleAsync(callback, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(10, "Молоко", null, "Alice", null), Times.Once);

        // Dialog cleared
        Assert.Null(dialogService.GetState(-100L, 42L));

        tagCaptureServiceMock.Verify(s => s.StartTagCaptureAsync(
            -100L, 42L, 10, It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 1), "Молоко",
            It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuySkipExpiryCallbackHandler_Saves_Item_Without_Expiry()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState
        {
            Step = 3,
            GroupId = 10,
            Name = "Молоко",
            Quantity = "2л",
            AddedByName = "Alice",
        });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(10, "Молоко", "2л", "Alice", null))
            .ReturnsAsync(new ShoppingItem
            {
                Id = 1,
                GroupId = 10,
                Name = "Молоко",
                Quantity = "2л",
                ExpDate = null,
                AddedByName = "Alice",
            });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(-100L, "buy.item-added-quantity"))
            .Returns("{name} added {item} ({quantity})");

        var handler = new BuySkipExpiryCallbackHandler(
            bot.Object,
            dialogService,
            itemRepo.Object,
            Mock.Of<IHistoryRepository>(),
            localizer.Object,
            Mock.Of<ILogger<BuySkipExpiryCallbackHandler>>());

        var callback = DeserializeCallbackQuery("{\"id\":\"cb1\",\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat_instance\":\"123\",\"message\":{\"message_id\":3,\"chat\":{\"id\":-100}}}");
        await handler.HandleAsync(callback, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(10, "Молоко", "2л", "Alice", null), Times.Once);

        var state = dialogService.GetState(-100L, 42L);
        Assert.Null(state);
    }
}
