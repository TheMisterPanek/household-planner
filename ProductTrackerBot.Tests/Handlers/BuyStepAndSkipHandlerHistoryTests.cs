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
    public async Task BuyStepHandler_Calls_RecordAsync_ItemAdded_After_Step3()
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
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Молоко", Quantity = "2л", ExpDate = expDate, AddedByName = "Alice" });

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.item-added-quantity-expiry"))
            .Returns("{name} added {item} ({quantity}, until {expiry})");

        var handler = new BuyStepHandler(bot.Object, dialogService, itemRepo.Object, historyMock.Object, localizer.Object, Mock.Of<ILogger<BuyStepHandler>>());
        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"5\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        historyMock.Verify(
            h => h.RecordAsync(-100L, 42L, "Alice", BotActionType.ItemAdded, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuyStepHandler_Swallows_RecordAsync_Failure()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState { Step = 3, GroupId = 10, Name = "Хлеб", Quantity = "1шт", AddedByName = "Alice" });

        var expDate = DateOnly.FromDateTime(DateTime.Now).AddDays(3);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()))
            .ReturnsAsync(new ShoppingItem { Id = 2, GroupId = 10, Name = "Хлеб", Quantity = "1шт", ExpDate = expDate, AddedByName = "Alice" });

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.item-added-quantity-expiry"))
            .Returns("{name} added {item} ({quantity}, until {expiry})");

        var handler = new BuyStepHandler(bot.Object, dialogService, itemRepo.Object, historyMock.Object, localizer.Object, Mock.Of<ILogger<BuyStepHandler>>());
        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"3\"}");

        await handler.HandleAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task BuySkipExpiryCallbackHandler_Calls_RecordAsync_ItemAdded()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState { Step = 3, GroupId = 10, Name = "Яйца", Quantity = "10шт", AddedByName = "Alice" });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()))
            .ReturnsAsync(new ShoppingItem { Id = 3, GroupId = 10, Name = "Яйца", Quantity = "10шт", AddedByName = "Alice" });

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.item-added-quantity"))
            .Returns("{name} added {item} ({quantity})");

        var handler = new BuySkipExpiryCallbackHandler(bot.Object, dialogService, itemRepo.Object, historyMock.Object, localizer.Object, Mock.Of<ILogger<BuySkipExpiryCallbackHandler>>());
        var callbackQuery = DeserializeCallbackQuery("{\"id\":\"cb1\",\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"message\":{\"message_id\":5,\"chat\":{\"id\":-100},\"text\":\"Дата?\"},\"data\":\"buy:skip_expiry\"}");

        await handler.HandleAsync(callbackQuery, CancellationToken.None);

        historyMock.Verify(
            h => h.RecordAsync(-100L, 42L, "Alice", BotActionType.ItemAdded, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BuySkipExpiryCallbackHandler_Swallows_RecordAsync_Failure()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<BuyDialogState>();
        dialogService.SetState(-100L, 42L, new BuyDialogState { Step = 3, GroupId = 10, Name = "Чай", Quantity = null, AddedByName = "Bob" });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()))
            .ReturnsAsync(new ShoppingItem { Id = 4, GroupId = 10, Name = "Чай", AddedByName = "Bob" });

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.item-added"))
            .Returns("{name} added {item}");

        var handler = new BuySkipExpiryCallbackHandler(bot.Object, dialogService, itemRepo.Object, historyMock.Object, localizer.Object, Mock.Of<ILogger<BuySkipExpiryCallbackHandler>>());
        var callbackQuery = DeserializeCallbackQuery("{\"id\":\"cb2\",\"from\":{\"id\":42,\"first_name\":\"Bob\"},\"message\":{\"message_id\":6,\"chat\":{\"id\":-100},\"text\":\"Дата?\"},\"data\":\"buy:skip_expiry\"}");

        await handler.HandleAsync(callbackQuery, CancellationToken.None);
    }
}
