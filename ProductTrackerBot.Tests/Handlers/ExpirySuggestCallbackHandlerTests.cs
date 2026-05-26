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

public class ExpirySuggestCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery SuggestCallback(long chatId, long userId, int days) =>
        JsonSerializer.Deserialize<CallbackQuery>(
            $"{{\"id\":\"cb1\",\"from\":{{\"id\":{userId},\"first_name\":\"Alice\"}},\"message\":{{\"message_id\":5,\"chat\":{{\"id\":{chatId},\"type\":\"supergroup\"}}}},\"data\":\"expiry:suggest:{days}\"}}",
            JsonOpts)!;

    private (
        ExpirySuggestCallbackHandler Handler,
        Mock<ITelegramBotClient> Bot,
        PendingDialogService<BoughtDialogState> BoughtDialog,
        PendingDialogService<PriceCaptureDialogState> PriceDialog,
        BoughtStepHandler BoughtStepHandler,
        PriceCaptureStepHandler PriceStepHandler,
        Mock<PurchaseHistoryRepository> PurchaseRepo) CreateHandler()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.AddAsync(It.IsAny<PurchaseRecord>()))
            .ReturnsAsync<PurchaseRecord, PurchaseHistoryRepository, PurchaseRecord>(r => r);
        purchaseRepo.Setup(r => r.GetExpiryDaySuggestionsAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<int>().AsReadOnly());
        purchaseRepo.Setup(r => r.GetAverageExpiryDaysAsync(It.IsAny<int>()))
            .ReturnsAsync(0);

        var historyRepo = new Mock<IHistoryRepository>();
        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file::memory:");
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var suggestionService = new ExpiryDaySuggestionService(purchaseRepo.Object);

        var boughtDialog = new PendingDialogService<BoughtDialogState>();
        var priceDialog = new PendingDialogService<PriceCaptureDialogState>();

        var boughtStepHandler = new BoughtStepHandler(
            bot.Object, boughtDialog, purchaseRepo.Object, historyRepo.Object,
            suggestionService, localizer.Object, Mock.Of<ILogger<BoughtStepHandler>>());

        var priceStepHandler = new PriceCaptureStepHandler(
            bot.Object, priceDialog, purchaseRepo.Object, priceLogRepo.Object,
            groupRepo.Object, suggestionService, localizer.Object, Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var handler = new ExpirySuggestCallbackHandler(
            bot.Object, boughtDialog, priceDialog, boughtStepHandler, priceStepHandler);

        return (handler, bot, boughtDialog, priceDialog, boughtStepHandler, priceStepHandler, purchaseRepo);
    }

    [Fact]
    public async Task RoutesToBoughtStepHandler_WhenBoughtDialogActiveAtStep2()
    {
        var (handler, bot, boughtDialog, _, _, _, purchaseRepo) = CreateHandler();
        boughtDialog.SetState(-100L, 42L, new BoughtDialogState
        {
            Step = 2,
            GroupId = 1,
            ItemName = "milk",
            BoughtByName = "Alice",
        });

        await handler.HandleAsync(SuggestCallback(-100L, 42L, 7), CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ItemName == "milk" &&
            p.ExpDate.HasValue)), Times.Once);
        Assert.Null(boughtDialog.GetState(-100L, 42L));
    }

    [Fact]
    public async Task RoutesToPriceCaptureStepHandler_WhenPriceDialogActiveAtStep3()
    {
        var (handler, bot, _, priceDialog, _, _, purchaseRepo) = CreateHandler();
        priceDialog.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 3,
            GroupId = 1,
            ItemName = "yogurt",
            BoughtByName = "Alice",
        });

        await handler.HandleAsync(SuggestCallback(-100L, 42L, 14), CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ItemName == "yogurt" &&
            p.ExpDate.HasValue)), Times.Once);
        Assert.Null(priceDialog.GetState(-100L, 42L));
    }

    [Fact]
    public async Task AnswersSilently_WhenNoDialogActive()
    {
        var (handler, bot, _, _, _, _, purchaseRepo) = CreateHandler();

        await handler.HandleAsync(SuggestCallback(-100L, 42L, 7), CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.IsAny<PurchaseRecord>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ParsesDaysCorrectly_FromCallbackData()
    {
        var (handler, bot, boughtDialog, _, _, _, purchaseRepo) = CreateHandler();
        boughtDialog.SetState(-100L, 42L, new BoughtDialogState
        {
            Step = 2,
            GroupId = 1,
            ItemName = "bread",
            BoughtByName = "Alice",
        });

        await handler.HandleAsync(SuggestCallback(-100L, 42L, 30), CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ExpDate.HasValue &&
            p.ExpDate!.Value >= DateOnly.FromDateTime(DateTime.Today.AddDays(29)) &&
            p.ExpDate!.Value <= DateOnly.FromDateTime(DateTime.Today.AddDays(31)))), Times.Once);
    }
}
