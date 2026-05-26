using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class UseRemoveCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery MakeCallback(long chatId, int messageId, string data) =>
        JsonSerializer.Deserialize<CallbackQuery>(
            $"{{\"id\":\"cb1\",\"from\":{{\"id\":1,\"first_name\":\"Alice\"}},\"message\":{{\"message_id\":{messageId},\"chat\":{{\"id\":{chatId},\"type\":\"supergroup\"}}}},\"data\":\"{data}\"}}",
            JsonOpts)!;

    private (
        UseRemoveCallbackHandler Handler,
        Mock<ITelegramBotClient> Bot,
        Mock<PurchaseHistoryRepository> PurchaseRepo,
        Mock<ShoppingItemRepository> ItemRepo)
    CreateHandler(IReadOnlyList<PurchaseRecord>? remainingPh = null, IReadOnlyList<ShoppingItem>? remainingSi = null)
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageReplyMarkupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var groupRepo = new Mock<GroupRepository>("Data Source=:memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync(new Group { Id = 1, ChatId = -100L });

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=:memory:");
        purchaseRepo.Setup(r => r.DeleteAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        purchaseRepo.Setup(r => r.GetInventoryItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(remainingPh ?? Array.Empty<PurchaseRecord>().ToList().AsReadOnly());

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=:memory:");
        itemRepo.Setup(r => r.DeleteAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(remainingSi ?? Array.Empty<ShoppingItem>().ToList().AsReadOnly());

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new UseRemoveCallbackHandler(
            bot.Object, groupRepo.Object, purchaseRepo.Object, itemRepo.Object,
            localizer.Object, Mock.Of<ILogger<UseRemoveCallbackHandler>>());

        return (handler, bot, purchaseRepo, itemRepo);
    }

    [Fact]
    public async Task PhCallback_CallsDeleteOnPurchaseRepo()
    {
        var (handler, _, purchaseRepo, itemRepo) = CreateHandler();

        await handler.HandleAsync(MakeCallback(-100L, 1, "use:remove:ph:42"), CancellationToken.None);

        purchaseRepo.Verify(r => r.DeleteAsync(42), Times.Once);
        itemRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SiCallback_CallsDeleteOnItemRepo()
    {
        var (handler, _, purchaseRepo, itemRepo) = CreateHandler();

        await handler.HandleAsync(MakeCallback(-100L, 1, "use:remove:si:7"), CancellationToken.None);

        itemRepo.Verify(r => r.DeleteAsync(7), Times.Once);
        purchaseRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AfterDelete_WhenItemsRemain_EditsKeyboard()
    {
        var remaining = new List<PurchaseRecord>
        {
            new()
            {
                Id = 99, GroupId = 1, UserId = 1, ItemName = "Butter",
                ExpDate = new DateOnly(2026, 6, 5), BoughtByName = "Alice", PurchasedAt = DateTime.UtcNow,
            },
        }.AsReadOnly();

        var (handler, bot, _, _) = CreateHandler(remainingPh: remaining);

        await handler.HandleAsync(MakeCallback(-100L, 5, "use:remove:ph:42"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<EditMessageReplyMarkupRequest>(r => r.MessageId == 5),
            It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AfterDelete_WhenNoItemsLeft_EditsToEmptyMessage()
    {
        var (handler, bot, _, _) = CreateHandler(); // empty remaining by default

        await handler.HandleAsync(MakeCallback(-100L, 5, "use:remove:ph:42"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<EditMessageTextRequest>(r => r.Text == "use.empty" && r.MessageId == 5),
            It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<EditMessageReplyMarkupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnswersCallbackWithToast()
    {
        var (handler, bot, _, _) = CreateHandler();

        await handler.HandleAsync(MakeCallback(-100L, 1, "use:remove:ph:1"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "use.remove-toast"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidCallbackData_DoesNothing()
    {
        var (handler, bot, purchaseRepo, itemRepo) = CreateHandler();

        // Missing id part
        await handler.HandleAsync(MakeCallback(-100L, 1, "use:remove:ph:notanumber"), CancellationToken.None);

        purchaseRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnknownSource_DoesNothing()
    {
        var (handler, bot, purchaseRepo, itemRepo) = CreateHandler();

        await handler.HandleAsync(MakeCallback(-100L, 1, "use:remove:xx:5"), CancellationToken.None);

        purchaseRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        itemRepo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CallbackPrefix_IsCorrect()
    {
        var (handler, _, _, _) = CreateHandler();
        Assert.Equal("use:remove:", handler.CallbackPrefix);
    }
}
