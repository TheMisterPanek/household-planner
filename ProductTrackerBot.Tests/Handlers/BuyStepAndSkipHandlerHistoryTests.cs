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
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class BuyStepAndSkipHandlerHistoryTests
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
        botMock.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
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
            IsOneLineMode = false,
            GroupId = 10,
            AddedByName = "Alice",
        });

        var pendingAddService = new PendingAddService();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyStepHandler(bot.Object, dialogService, pendingAddService, localizer.Object);
        var message = DeserializeMessage("{\"message_id\":2,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"Молоко\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        // Dialog advanced to step 2 — "how much" message sent
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Equal("Молоко", state.Name);
    }

    [Fact]
    public async Task BuyStepHandler_Step1_OneLineMode_StoresInPendingAndSendsTwoButtonReview()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState
        {
            Step = 1,
            IsOneLineMode = true,
            GroupId = 10,
            AddedByName = "Alice",
        });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        var pendingAddService = new PendingAddService();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyStepHandler(bot.Object, dialogService, pendingAddService, localizer.Object);
        var message = DeserializeMessage("{\"message_id\":2,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"отривин 1 шт\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        // AddAsync must NOT be called
        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), null), Times.Never);

        // Review message sent
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        // Dialog state cleared
        Assert.Null(dialogService.GetState(-100L, 42L));
    }

    [Fact]
    public async Task BuyStepHandler_Step2_StoresInPendingService_DoesNotCallAddAsync()
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
        var pendingAddService = new PendingAddService();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyStepHandler(bot.Object, dialogService, pendingAddService, localizer.Object);
        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"2л\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        // AddAsync must NOT be called — item is now pending until confirmed
        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), null), Times.Never);

        // Review message was sent
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuyStepHandler_Step2_SendsThreeButtonReview()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState
        {
            Step = 2,
            GroupId = 10,
            Name = "Хлеб",
            AddedByName = "Alice",
        });

        var pendingAddService = new PendingAddService();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyStepHandler(bot.Object, dialogService, pendingAddService, localizer.Object);
        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"1шт\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BuySkipExpiryCallbackHandler_Calls_RecordAsync_ItemAdded()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState { Step = 3, GroupId = 10, Name = "Яйца", Quantity = "10шт", AddedByName = "Alice" });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), null))
            .ReturnsAsync(new ShoppingItem { Id = 3, GroupId = 10, Name = "Яйца", Quantity = "10шт", AddedByName = "Alice" });

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.item-added-quantity"))
            .Returns("{name} added {item} ({quantity})");

        var handler = new BuySkipExpiryCallbackHandler(bot.Object, dialogService, itemRepo.Object, historyMock.Object, localizer.Object, Mock.Of<ILogger<BuySkipExpiryCallbackHandler>>());
        var callbackQuery = DeserializeCallbackQuery("{\"id\":\"cb1\",\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"message\":{\"message_id\":5,\"chat\":{\"id\":-100},\"text\":\"Дата?\"},\"data\":\"buy:skip_expiry\"}");

        await handler.HandleAsync(callbackQuery, CancellationToken.None);

        historyMock.Verify(
            h => h.RecordAsync(-100L, 42L, "Alice", BotActionType.ItemAdded, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuySkipExpiryCallbackHandler_Swallows_RecordAsync_Failure()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState { Step = 3, GroupId = 10, Name = "Чай", Quantity = null, AddedByName = "Bob" });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), null))
            .ReturnsAsync(new ShoppingItem { Id = 4, GroupId = 10, Name = "Чай", AddedByName = "Bob" });

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.item-added"))
            .Returns("{name} added {item}");

        var handler = new BuySkipExpiryCallbackHandler(bot.Object, dialogService, itemRepo.Object, historyMock.Object, localizer.Object, Mock.Of<ILogger<BuySkipExpiryCallbackHandler>>());
        var callbackQuery = DeserializeCallbackQuery("{\"id\":\"cb2\",\"from\":{\"id\":42,\"first_name\":\"Bob\"},\"message\":{\"message_id\":6,\"chat\":{\"id\":-100},\"text\":\"Дата?\"},\"data\":\"buy:skip_expiry\"}");

        await handler.HandleAsync(callbackQuery, CancellationToken.None);
    }
}
