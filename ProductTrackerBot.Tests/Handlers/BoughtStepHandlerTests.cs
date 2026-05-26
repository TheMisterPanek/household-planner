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

public class BoughtStepHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Message GroupMessage(long chatId, long userId, string text) =>
        DeserializeMessage($"{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"Alice\"}},\"chat\":{{\"id\":{chatId},\"type\":\"supergroup\"}},\"text\":\"{text}\"}}");

    private (BoughtStepHandler Handler, Mock<ITelegramBotClient> Bot, PendingDialogService<BoughtDialogState> DialogService, Mock<PurchaseHistoryRepository> PurchaseRepo) CreateHandler()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var dialogService = new PendingDialogService<BoughtDialogState>();

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.AddAsync(It.IsAny<PurchaseRecord>()))
            .ReturnsAsync<PurchaseRecord, PurchaseHistoryRepository, PurchaseRecord>(r => r);

        var historyRepo = new Mock<IHistoryRepository>();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);
        localizer.Setup(l => l.Get(It.IsAny<long>(), "bought.done"))
            .Returns("✓ {item} registered");
        localizer.Setup(l => l.Get(It.IsAny<long>(), "bought.done-with-expiry"))
            .Returns("✓ {item} registered, expires {expiry}");

        var suggestionRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        suggestionRepo.Setup(r => r.GetExpiryDaySuggestionsAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<int>().AsReadOnly());
        suggestionRepo.Setup(r => r.GetAverageExpiryDaysAsync(It.IsAny<int>()))
            .ReturnsAsync(0);
        var suggestionService = new ExpiryDaySuggestionService(suggestionRepo.Object);

        var handler = new BoughtStepHandler(
            bot.Object, dialogService, purchaseRepo.Object, historyRepo.Object,
            suggestionService, localizer.Object, Mock.Of<ILogger<BoughtStepHandler>>());

        return (handler, bot, dialogService, purchaseRepo);
    }

    [Fact]
    public async Task Step1_ParsesItemAndQuantity_AdvancesToStep2_SendsExpiryPrompt()
    {
        var (handler, bot, dialogService, _) = CreateHandler();
        dialogService.SetState(-100L, 42L, new BoughtDialogState { Step = 1, GroupId = 10, BoughtByName = "Alice" });

        await handler.HandleAsync(GroupMessage(-100L, 42L, "eggs 12"), CancellationToken.None);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Equal("eggs", state.ItemName);
        Assert.Equal("12", state.Quantity);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Step2_ValidText_CallsAddAsync_ClearsState_ReplyContainsItem()
    {
        var (handler, bot, dialogService, purchaseRepo) = CreateHandler();
        dialogService.SetState(-100L, 42L, new BoughtDialogState
        {
            Step = 2, GroupId = 10, ItemName = "milk", Quantity = "1L", BoughtByName = "Alice",
        });

        await handler.HandleAsync(GroupMessage(-100L, 42L, "7"), CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.IsAny<PurchaseRecord>()), Times.Once);
        Assert.Null(dialogService.GetState(-100L, 42L));

        bot.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(r =>
            r.Text.Contains("milk")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Step2_WithExpiry_ReplyContainsFormattedDate()
    {
        var (handler, bot, dialogService, _) = CreateHandler();
        dialogService.SetState(-100L, 42L, new BoughtDialogState
        {
            Step = 2, GroupId = 10, ItemName = "juice", BoughtByName = "Alice",
        });

        await handler.HandleAsync(GroupMessage(-100L, 42L, "14"), CancellationToken.None);

        var expectedDate = DateOnly.FromDateTime(DateTime.Now).AddDays(14).ToString("dd.MM.yyyy");
        bot.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(r =>
            r.Text.Contains(expectedDate)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Step2_InvalidText_SendsError_StateRemainsStep2()
    {
        var (handler, bot, dialogService, purchaseRepo) = CreateHandler();
        dialogService.SetState(-100L, 42L, new BoughtDialogState
        {
            Step = 2, GroupId = 10, ItemName = "bread", BoughtByName = "Alice",
        });

        await handler.HandleAsync(GroupMessage(-100L, 42L, "notadate"), CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.IsAny<PurchaseRecord>()), Times.Never);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);

        bot.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(r =>
            r.Text == "shop.invalid-date"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
