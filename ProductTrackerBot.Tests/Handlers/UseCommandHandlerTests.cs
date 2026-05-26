using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class UseCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message MakeMessage(long chatId, string text = "/use") =>
        JsonSerializer.Deserialize<Message>(
            $"{{\"message_id\":1,\"from\":{{\"id\":1,\"first_name\":\"Alice\"}},\"chat\":{{\"id\":{chatId},\"type\":\"supergroup\"}},\"text\":\"{text}\"}}",
            JsonOpts)!;

    private static Group MakeGroup(int id = 1) =>
        new() { Id = id, ChatId = -100L };

    private (UseCommandHandler Handler, Mock<ITelegramBotClient> Bot, Mock<PurchaseHistoryRepository> PurchaseRepo, Mock<ShoppingItemRepository> ItemRepo) CreateHandler(
        Group? group = null)
    {
        var g = group ?? MakeGroup();
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var groupRepo = new Mock<GroupRepository>("Data Source=:memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>())).ReturnsAsync(g);

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=:memory:");
        purchaseRepo.Setup(r => r.GetInventoryItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<PurchaseRecord>().ToList().AsReadOnly());

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=:memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<ShoppingItem>().ToList().AsReadOnly());

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new UseCommandHandler(
            bot.Object, groupRepo.Object, purchaseRepo.Object, itemRepo.Object, localizer.Object);

        return (handler, bot, purchaseRepo, itemRepo);
    }

    [Fact]
    public async Task WhenBothSourcesEmpty_SendsEmptyMessage()
    {
        var (handler, bot, _, _) = CreateHandler();

        await handler.HandleAsync(MakeMessage(-100L), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "use.empty" && r.ReplyMarkup == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenPhItemExists_SendsMessageWithRemoveButton()
    {
        var (handler, bot, purchaseRepo, _) = CreateHandler();
        var record = new PurchaseRecord
        {
            Id = 42,
            GroupId = 1,
            UserId = 1,
            ItemName = "Milk",
            Quantity = "1L",
            ExpDate = new DateOnly(2026, 6, 1),
            BoughtByName = "Alice",
            PurchasedAt = DateTime.UtcNow,
        };
        purchaseRepo.Setup(r => r.GetInventoryItemsWithExpiryAsync(1))
            .ReturnsAsync(new List<PurchaseRecord> { record }.AsReadOnly());

        await handler.HandleAsync(MakeMessage(-100L), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text != null && r.Text.Contains("use.header") &&
                r.ReplyMarkup is Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PhItemButton_HasCorrectCallbackPrefix()
    {
        var (handler, bot, purchaseRepo, _) = CreateHandler();
        var record = new PurchaseRecord
        {
            Id = 7,
            GroupId = 1,
            UserId = 1,
            ItemName = "Cheese",
            ExpDate = new DateOnly(2026, 6, 5),
            BoughtByName = "Alice",
            PurchasedAt = DateTime.UtcNow,
        };
        purchaseRepo.Setup(r => r.GetInventoryItemsWithExpiryAsync(1))
            .ReturnsAsync(new List<PurchaseRecord> { record }.AsReadOnly());

        SendMessageRequest? captured = null;
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Telegram.Bot.Requests.Abstractions.IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) captured = smr;
            })
            .ReturnsAsync(new Message());

        await handler.HandleAsync(MakeMessage(-100L), CancellationToken.None);

        Assert.NotNull(captured);
        var keyboard = captured.ReplyMarkup as Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup;
        Assert.NotNull(keyboard);
        var callbackData = keyboard.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).ToList();
        Assert.Contains(callbackData, d => d == "use:remove:ph:7");
    }

    [Fact]
    public async Task SiItemButton_HasCorrectCallbackPrefix()
    {
        var (handler, bot, _, itemRepo) = CreateHandler();
        var item = new ShoppingItem
        {
            Id = 3,
            GroupId = 1,
            Name = "Butter",
            ExpDate = new DateOnly(2026, 6, 3),
            AddedByName = "Bob",
        };
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(1))
            .ReturnsAsync(new List<ShoppingItem> { item }.AsReadOnly());

        SendMessageRequest? captured = null;
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Telegram.Bot.Requests.Abstractions.IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) captured = smr;
            })
            .ReturnsAsync(new Message());

        await handler.HandleAsync(MakeMessage(-100L), CancellationToken.None);

        var keyboard = captured?.ReplyMarkup as Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup;
        Assert.NotNull(keyboard);
        var callbackData = keyboard.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).ToList();
        Assert.Contains(callbackData, d => d == "use:remove:si:3");
    }

    [Fact]
    public async Task Items_AreSortedByExpiryDate()
    {
        var (handler, bot, purchaseRepo, itemRepo) = CreateHandler();

        var older = new PurchaseRecord
        {
            Id = 1, GroupId = 1, UserId = 1, ItemName = "Yogurt",
            ExpDate = new DateOnly(2026, 5, 28), BoughtByName = "Alice", PurchasedAt = DateTime.UtcNow,
        };
        var newer = new PurchaseRecord
        {
            Id = 2, GroupId = 1, UserId = 1, ItemName = "Cream",
            ExpDate = new DateOnly(2026, 6, 10), BoughtByName = "Alice", PurchasedAt = DateTime.UtcNow,
        };
        purchaseRepo.Setup(r => r.GetInventoryItemsWithExpiryAsync(1))
            .ReturnsAsync(new List<PurchaseRecord> { newer, older }.AsReadOnly());

        SendMessageRequest? captured = null;
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Telegram.Bot.Requests.Abstractions.IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) captured = smr;
            })
            .ReturnsAsync(new Message());

        await handler.HandleAsync(MakeMessage(-100L), CancellationToken.None);

        var keyboard = captured?.ReplyMarkup as Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup;
        var rows = keyboard?.InlineKeyboard.ToList();
        Assert.NotNull(rows);
        Assert.Equal(2, rows.Count);
        // First row should be older (Yogurt, 28.05.2026)
        Assert.Contains("28.05.2026", rows[0].First().Text ?? string.Empty);
        Assert.Contains("10.06.2026", rows[1].First().Text ?? string.Empty);
    }

    [Fact]
    public void Command_IsSlashUse()
    {
        var (handler, _, _, _) = CreateHandler();
        Assert.Equal("/use", handler.Command);
    }
}
