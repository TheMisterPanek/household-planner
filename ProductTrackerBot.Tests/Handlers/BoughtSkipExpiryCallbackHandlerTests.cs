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

public class BoughtSkipExpiryCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery DeserializeCallback(string json) =>
        JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;

    private static CallbackQuery SkipCallback(long chatId, long userId) =>
        DeserializeCallback($"{{\"id\":\"cb1\",\"from\":{{\"id\":{userId},\"first_name\":\"Alice\"}},\"message\":{{\"message_id\":5,\"chat\":{{\"id\":{chatId},\"type\":\"supergroup\"}}}},\"data\":\"bought:skip_expiry\"}}");

    private (BoughtSkipExpiryCallbackHandler Handler, Mock<ITelegramBotClient> Bot, PendingDialogService<BoughtDialogState> DialogService, Mock<PurchaseHistoryRepository> PurchaseRepo) CreateHandler()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dialogService = new PendingDialogService<BoughtDialogState>();

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.AddAsync(It.IsAny<PurchaseRecord>()))
            .ReturnsAsync<PurchaseRecord, PurchaseHistoryRepository, PurchaseRecord>(r => r);

        var historyRepo = new Mock<IHistoryRepository>();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BoughtSkipExpiryCallbackHandler(
            bot.Object, dialogService, purchaseRepo.Object, historyRepo.Object,
            localizer.Object, Mock.Of<ILogger<BoughtSkipExpiryCallbackHandler>>());

        return (handler, bot, dialogService, purchaseRepo);
    }

    [Fact]
    public async Task SkipWithActiveState_AddsRecord_ExpDateNull_ClearsState()
    {
        var (handler, bot, dialogService, purchaseRepo) = CreateHandler();
        dialogService.SetState(-100L, 42L, new BoughtDialogState
        {
            Step = 2, GroupId = 10, ItemName = "eggs", Quantity = null, BoughtByName = "Alice",
        });

        await handler.HandleAsync(SkipCallback(-100L, 42L), CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(rec =>
            rec.ItemName == "eggs" && rec.ExpDate == null)), Times.Once);
        Assert.Null(dialogService.GetState(-100L, 42L));
    }

    [Fact]
    public async Task SkipWithNoState_AnswersCallback_NoRepositoryCall()
    {
        var (handler, bot, dialogService, purchaseRepo) = CreateHandler();

        await handler.HandleAsync(SkipCallback(-100L, 42L), CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.IsAny<PurchaseRecord>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
