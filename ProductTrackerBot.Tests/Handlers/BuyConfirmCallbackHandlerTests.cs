using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using ProductTrackerBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class BuyConfirmCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery DeserializeCallbackQuery(string json) =>
        JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return bot;
    }

    private static Mock<CategoryCaptureService> CreateCategoryCaptureServiceMock(Mock<ITelegramBotClient> bot, Mock<ILocalizer> localizer)
    {
        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetTopCategoriesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());
        var mock = new Mock<CategoryCaptureService>(bot.Object, new PendingDialogService<CategoryCaptureDialogState>(), purchaseRepo.Object, localizer.Object);
        mock.Setup(s => s.StartCategoryCaptureAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task ValidToken_CallsAddAsync_RecordsHistory_SendsConfirm()
    {
        var bot = CreateBotMock();
        var pendingAddService = new PendingAddService();
        var token = pendingAddService.Store(new PendingAddItem(
            ChatId: -100L,
            GroupId: 10,
            Name: "Молоко",
            Quantity: "2 л",
            AddedByName: "Alice"));

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(10, "Молоко", "2 л", "Alice", null, null))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Молоко", Quantity = "2 л", AddedByName = "Alice" });

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var categoryCaptureServiceMock = CreateCategoryCaptureServiceMock(bot, localizer);
        var buyAddService = new BuyAddService(
            bot.Object, itemRepo.Object, historyMock.Object, categoryCaptureServiceMock.Object,
            localizer.Object, Mock.Of<ILogger<BuyAddService>>());

        var handler = new BuyConfirmCallbackHandler(
            bot.Object, pendingAddService, buyAddService);

        var cbQuery = DeserializeCallbackQuery(
            $"{{\"id\":\"cb1\",\"from\":{{\"id\":42,\"first_name\":\"Alice\"}}," +
            $"\"message\":{{\"message_id\":5,\"chat\":{{\"id\":-100}},\"text\":\"test\"}}," +
            $"\"data\":\"buy:confirm:{token}\"}}");

        await handler.HandleAsync(cbQuery, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(10, "Молоко", "2 л", "Alice", null, null), Times.Once);
        historyMock.Verify(h => h.RecordAsync(
            -100L, 42L, "Alice", BotActionType.ItemAdded,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        categoryCaptureServiceMock.Verify(s => s.StartCategoryCaptureAsync(
            -100L, 42L, 10, It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 1), "Молоко", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExpiredToken_AnswersWithError_DoesNotCallAddAsync()
    {
        var bot = CreateBotMock();
        var pendingAddService = new PendingAddService();

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        var historyMock = new Mock<IHistoryRepository>();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var buyAddService = new BuyAddService(
            bot.Object, itemRepo.Object, historyMock.Object, CreateCategoryCaptureServiceMock(bot, localizer).Object,
            localizer.Object, Mock.Of<ILogger<BuyAddService>>());

        var handler = new BuyConfirmCallbackHandler(
            bot.Object, pendingAddService, buyAddService);

        var cbQuery = DeserializeCallbackQuery(
            "{\"id\":\"cb2\",\"from\":{\"id\":42,\"first_name\":\"Alice\"}," +
            "\"message\":{\"message_id\":5,\"chat\":{\"id\":-100},\"text\":\"test\"}," +
            "\"data\":\"buy:confirm:deadbeef\"}");

        await handler.HandleAsync(cbQuery, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), null), Times.Never);
        historyMock.Verify(h => h.RecordAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
