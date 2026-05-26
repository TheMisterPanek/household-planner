using Moq;
using ProductTrackerBot.Models;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class UseInventoryIntegrationTests : TelegramIntegrationTestBase
{
    private const long ChatId = -200L;
    private const long UserId = 1L;

    [Fact]
    public async Task UseCommand_WithNoItems_SendsEmptyMessage()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(ChatId, UserId, "/use"));

        BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "use.empty"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UseCommand_WithPhItem_SendsMessageWithRemoveButton()
    {
        await ClearDataAsync();
        var group = await GroupRepository.GetOrCreateAsync(ChatId);
        await PurchaseRepository.AddAsync(new PurchaseRecord
        {
            GroupId = group.Id,
            UserId = UserId,
            ItemName = "Milk",
            Quantity = "1L",
            ExpDate = new DateOnly(2026, 7, 1),
            BoughtByName = "Alice",
            PurchasedAt = DateTime.UtcNow,
        });

        SendMessageRequest? sent = null;
        BotMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Telegram.Bot.Requests.Abstractions.IRequest<Telegram.Bot.Types.Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sent = smr;
            })
            .ReturnsAsync(new Telegram.Bot.Types.Message());

        await DispatchAsync(CommandUpdate(ChatId, UserId, "/use"));

        Assert.NotNull(sent);
        Assert.Contains("use.header", sent.Text ?? string.Empty);
        var keyboard = sent.ReplyMarkup as Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup;
        Assert.NotNull(keyboard);
        var callbacks = keyboard.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).ToList();
        Assert.Contains(callbacks, c => c != null && c.StartsWith("use:remove:ph:"));
    }

    [Fact]
    public async Task UseRemoveCallback_Ph_DeletesItemAndEditsKeyboard()
    {
        await ClearDataAsync();
        var group = await GroupRepository.GetOrCreateAsync(ChatId);
        var record = await PurchaseRepository.AddAsync(new PurchaseRecord
        {
            GroupId = group.Id,
            UserId = UserId,
            ItemName = "Cheese",
            ExpDate = new DateOnly(2026, 7, 5),
            BoughtByName = "Alice",
            PurchasedAt = DateTime.UtcNow,
        });

        await DispatchAsync(CallbackUpdate(ChatId, UserId, 10, $"use:remove:ph:{record.Id}"));

        // Item is deleted
        var remaining = await PurchaseRepository.GetInventoryItemsWithExpiryAsync(group.Id);
        Assert.Empty(remaining);

        // Since no items remain, message is edited to empty text
        BotMock.Verify(b => b.SendRequest(
            It.Is<EditMessageTextRequest>(r => r.Text == "use.empty" && r.MessageId == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UseRemoveCallback_WhenItemsRemain_EditsKeyboard()
    {
        await ClearDataAsync();
        var group = await GroupRepository.GetOrCreateAsync(ChatId);
        var r1 = await PurchaseRepository.AddAsync(new PurchaseRecord
        {
            GroupId = group.Id,
            UserId = UserId,
            ItemName = "Milk",
            ExpDate = new DateOnly(2026, 7, 1),
            BoughtByName = "Alice",
            PurchasedAt = DateTime.UtcNow,
        });
        await PurchaseRepository.AddAsync(new PurchaseRecord
        {
            GroupId = group.Id,
            UserId = UserId,
            ItemName = "Cheese",
            ExpDate = new DateOnly(2026, 7, 5),
            BoughtByName = "Alice",
            PurchasedAt = DateTime.UtcNow,
        });

        await DispatchAsync(CallbackUpdate(ChatId, UserId, 10, $"use:remove:ph:{r1.Id}"));

        // Only one item remains
        var remaining = await PurchaseRepository.GetInventoryItemsWithExpiryAsync(group.Id);
        Assert.Single(remaining);

        // Keyboard is edited (not text)
        BotMock.Verify(b => b.SendRequest(
            It.Is<EditMessageReplyMarkupRequest>(r => r.MessageId == 10),
            It.IsAny<CancellationToken>()), Times.Once);
        BotMock.Verify(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UseCommand_WithSiItem_IncludesInKeyboard()
    {
        await ClearDataAsync();
        var group = await GroupRepository.GetOrCreateAsync(ChatId);
        // Add a shopping item with expiry
        await ItemRepository.AddAsync(group.Id, "Butter", null, "Bob", new DateOnly(2026, 7, 10));

        SendMessageRequest? sent = null;
        BotMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Telegram.Bot.Requests.Abstractions.IRequest<Telegram.Bot.Types.Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sent = smr;
            })
            .ReturnsAsync(new Telegram.Bot.Types.Message());

        await DispatchAsync(CommandUpdate(ChatId, UserId, "/use"));

        var keyboard = sent?.ReplyMarkup as Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup;
        Assert.NotNull(keyboard);
        var callbacks = keyboard.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).ToList();
        Assert.Contains(callbacks, c => c != null && c.StartsWith("use:remove:si:"));
    }
}
