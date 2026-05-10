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
    public async Task BuyStepHandler_HandleStep3_Valid_Days_Number_Saves_Item_With_Expiry()
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

        var expDate = DateOnly.FromDateTime(DateTime.Now).AddDays(5);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()))
            .ReturnsAsync(new ShoppingItem
            {
                Id = 1,
                GroupId = 10,
                Name = "Молоко",
                Quantity = "2л",
                ExpDate = expDate,
                AddedByName = "Alice",
            });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(-100L, "buy.item-added-quantity-expiry"))
            .Returns("{name} added {item} ({quantity}, until {expiry})");

        var handler = new BuyStepHandler(
            bot.Object,
            dialogService,
            itemRepo.Object,
            Mock.Of<IHistoryRepository>(),
            localizer.Object,
            Mock.Of<ILogger<BuyStepHandler>>());

        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"5\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(10, "Молоко", "2л", "Alice", It.IsAny<DateOnly?>()), Times.Once);
    }

    [Fact]
    public async Task BuyStepHandler_HandleStep3_Valid_Week_Variant_Saves_Item_With_Expiry()
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

        var weekExpDate = DateOnly.FromDateTime(DateTime.Now).AddDays(14);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()))
            .ReturnsAsync(new ShoppingItem
            {
                Id = 1,
                GroupId = 10,
                Name = "Молоко",
                Quantity = "2л",
                ExpDate = weekExpDate,
                AddedByName = "Alice",
            });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(-100L, "buy.item-added-quantity-expiry"))
            .Returns("{name} added {item} ({quantity}, until {expiry})");

        var handler = new BuyStepHandler(
            bot.Object,
            dialogService,
            itemRepo.Object,
            Mock.Of<IHistoryRepository>(),
            localizer.Object,
            Mock.Of<ILogger<BuyStepHandler>>());

        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"2 week\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(10, "Молоко", "2л", "Alice", It.IsAny<DateOnly?>()), Times.Once);
    }

    [Fact]
    public async Task BuyStepHandler_HandleStep3_Invalid_Input_Stays_At_Step3()
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

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(-100L, "buy.invalid-date"))
            .Returns("Invalid format");

        var handler = new BuyStepHandler(
            bot.Object,
            dialogService,
            itemRepo.Object,
            Mock.Of<IHistoryRepository>(),
            localizer.Object,
            Mock.Of<ILogger<BuyStepHandler>>());

        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"invalid\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly?>()), Times.Never);

        var updatedState = dialogService.GetState(-100L, 42L);
        Assert.NotNull(updatedState);
        Assert.Equal(3, updatedState.Step);
    }

    [Fact]
    public async Task BuySkipCallbackHandler_Transitions_To_Step3()
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

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:").Object;
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(-100L, "buy.skip"))
            .Returns("Skip");
        localizer.Setup(l => l.Get(-100L, "buy.expiry-date"))
            .Returns("Expiry date?");

        var handler = new BuySkipCallbackHandler(
            bot.Object,
            dialogService,
            itemRepo,
            Mock.Of<IHistoryRepository>(),
            localizer.Object,
            Mock.Of<ILogger<BuySkipCallbackHandler>>());

        var callback = DeserializeCallbackQuery("{\"id\":\"cb1\",\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat_instance\":\"123\",\"message\":{\"message_id\":2,\"chat\":{\"id\":-100}}}");
        await handler.HandleAsync(callback, CancellationToken.None);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(3, state.Step);
        Assert.Null(state.Quantity);
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
