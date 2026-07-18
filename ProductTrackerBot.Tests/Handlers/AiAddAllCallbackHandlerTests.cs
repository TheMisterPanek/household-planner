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

public class AiAddAllCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery MakeCallback(long chatId, long userId, string data) =>
        JsonSerializer.Deserialize<CallbackQuery>(
            $"{{\"id\":\"cb1\",\"from\":{{\"id\":{userId},\"first_name\":\"Alice\"}},\"message\":{{\"message_id\":1,\"chat\":{{\"id\":{chatId}}}}},\"data\":\"{data}\"}}",
            JsonOpts)!;

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return bot;
    }

    // Valid batch token → all items added, popup shows count, confirmation message sent
    [Fact]
    public async Task ValidBatchToken_AddsAllItems_SendsConfirmation()
    {
        var bot = CreateBotMock();
        var aiSuggestionService = new AiSuggestionService();
        var suggestions = new List<AiSuggestion>
        {
            new("pasta", "500g"),
            new("eggs", null),
        };
        var token = aiSuggestionService.StoreBatch(suggestions);

        var groupRepo = new Mock<GroupRepository>("Data Source=:memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 5, ChatId = -100, LanguageCode = "en" });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=:memory:");
        itemRepo.Setup(r => r.AddAsync(5, "pasta", "500g", "Alice", null))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 5, Name = "pasta", Quantity = "500g", AddedByName = "Alice" });
        itemRepo.Setup(r => r.AddAsync(5, "eggs", null, "Alice", null))
            .ReturnsAsync(new ShoppingItem { Id = 2, GroupId = 5, Name = "eggs", Quantity = null, AddedByName = "Alice" });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key == "ai.suggestion-all-added"
                ? "✅ Added {count} items!"
                : key == "ai.suggestion-all-added-msg"
                    ? "✅ Added {count} items to your list"
                    : key);

        var handler = new AiAddAllCallbackHandler(
            bot.Object, aiSuggestionService, groupRepo.Object, itemRepo.Object,
            Mock.Of<IHistoryRepository>(), localizer.Object,
            Mock.Of<ILogger<AiAddAllCallbackHandler>>());

        var callback = MakeCallback(-100L, 42L, $"ai:add-all:{token}");
        await handler.HandleAsync(callback, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(5, "pasta", "500g", "Alice", null), Times.Once);
        itemRepo.Verify(r => r.AddAsync(5, "eggs", null, "Alice", null), Times.Once);

        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text != null && r.Text.Contains("2")),
            It.IsAny<CancellationToken>()), Times.Once);

        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("2")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Expired/missing batch token → expiry message, nothing added
    [Fact]
    public async Task ExpiredBatchToken_SendsExpiryMessage_DoesNotAddItems()
    {
        var bot = CreateBotMock();
        var aiSuggestionService = new AiSuggestionService();
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=:memory:");

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new AiAddAllCallbackHandler(
            bot.Object, aiSuggestionService,
            new Mock<GroupRepository>("Data Source=:memory:").Object,
            itemRepo.Object,
            Mock.Of<IHistoryRepository>(), localizer.Object,
            Mock.Of<ILogger<AiAddAllCallbackHandler>>());

        var callback = MakeCallback(-100L, 42L, "ai:add-all:deadbeef");
        await handler.HandleAsync(callback, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()), Times.Never);
        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "ai.suggestion-expired"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Batch token is consumed after first tap (cannot double-add)
    [Fact]
    public async Task BatchToken_ConsumedAfterFirstTap_SecondTapShowsExpiry()
    {
        var bot = CreateBotMock();
        var aiSuggestionService = new AiSuggestionService();
        var suggestions = new List<AiSuggestion> { new("milk", "1L") };
        var token = aiSuggestionService.StoreBatch(suggestions);

        var groupRepo = new Mock<GroupRepository>("Data Source=:memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 5, ChatId = -100, LanguageCode = "en" });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=:memory:");
        itemRepo.Setup(r => r.AddAsync(5, "milk", "1L", "Alice", null))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 5, Name = "milk", Quantity = "1L", AddedByName = "Alice" });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new AiAddAllCallbackHandler(
            bot.Object, aiSuggestionService, groupRepo.Object, itemRepo.Object,
            Mock.Of<IHistoryRepository>(), localizer.Object,
            Mock.Of<ILogger<AiAddAllCallbackHandler>>());

        var callback = MakeCallback(-100L, 42L, $"ai:add-all:{token}");

        await handler.HandleAsync(callback, CancellationToken.None);
        // Second tap: batch token already consumed
        await handler.HandleAsync(callback, CancellationToken.None);

        // AddAsync called exactly once (first tap), not twice
        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()), Times.Once);
        // Second tap gets expiry popup
        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "ai.suggestion-expired"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
