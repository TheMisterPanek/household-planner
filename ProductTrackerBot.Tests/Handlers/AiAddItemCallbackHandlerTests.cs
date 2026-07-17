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

public class AiAddItemCallbackHandlerTests
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

    // Task 10.1: Valid token → AddAsync called, AnswerCallbackQuery with "Added!" popup, confirmation message sent
    [Fact]
    public async Task ValidToken_AddsItem_SendsConfirmation()
    {
        var bot = CreateBotMock();
        var aiSuggestionService = new AiSuggestionService();
        var token = aiSuggestionService.Store(new AiSuggestion("pasta", "500g"));

        var groupRepo = new Mock<GroupRepository>("Data Source=:memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 5, ChatId = -100, LanguageCode = "en" });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=:memory:");
        itemRepo.Setup(r => r.AddAsync(5, "pasta", "500g", "Alice", null, null))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 5, Name = "pasta", Quantity = "500g", AddedByName = "Alice" });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key == "ai.suggestion-added-msg-qty"
                ? "✅ Added: {item} ({count})"
                : key);

        var handler = new AiAddItemCallbackHandler(
            bot.Object, aiSuggestionService, groupRepo.Object, itemRepo.Object,
            Mock.Of<IHistoryRepository>(), localizer.Object,
            Mock.Of<ILogger<AiAddItemCallbackHandler>>());

        string? capturedText = null;
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Telegram.Bot.Requests.Abstractions.IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) capturedText = smr.Text;
            })
            .ReturnsAsync(new Message());

        var callback = MakeCallback(-100L, 42L, $"ai:add:{token}");
        await handler.HandleAsync(callback, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(5, "pasta", "500g", "Alice", null, null), Times.Once);
        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "ai.suggestion-added"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(capturedText);
        Assert.Contains("pasta", capturedText);
        Assert.Contains("500g", capturedText);
    }

    // Task 10.2: Expired/missing token → AnswerCallbackQuery with expired message, AddAsync NOT called
    [Fact]
    public async Task ExpiredToken_SendsExpiryMessage_DoesNotAddItem()
    {
        var bot = CreateBotMock();
        var aiSuggestionService = new AiSuggestionService();

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=:memory:");

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new AiAddItemCallbackHandler(
            bot.Object, aiSuggestionService,
            new Mock<GroupRepository>("Data Source=:memory:").Object,
            itemRepo.Object,
            Mock.Of<IHistoryRepository>(), localizer.Object,
            Mock.Of<ILogger<AiAddItemCallbackHandler>>());

        var callback = MakeCallback(-100L, 42L, "ai:add:deadbeef");
        await handler.HandleAsync(callback, CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), null), Times.Never);
        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "ai.suggestion-expired"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Task 10.3: Suggestion with no count → confirmation uses no-count key
    [Fact]
    public async Task SuggestionWithNoCount_UsesNoCountMessageKey()
    {
        var bot = CreateBotMock();
        var aiSuggestionService = new AiSuggestionService();
        var token = aiSuggestionService.Store(new AiSuggestion("eggs", null));

        var groupRepo = new Mock<GroupRepository>("Data Source=:memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 5, ChatId = -100, LanguageCode = "en" });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=:memory:");
        itemRepo.Setup(r => r.AddAsync(5, "eggs", null, "Alice", null, null))
            .ReturnsAsync(new ShoppingItem { Id = 2, GroupId = 5, Name = "eggs", Quantity = null, AddedByName = "Alice" });

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new AiAddItemCallbackHandler(
            bot.Object, aiSuggestionService, groupRepo.Object, itemRepo.Object,
            Mock.Of<IHistoryRepository>(), localizer.Object,
            Mock.Of<ILogger<AiAddItemCallbackHandler>>());

        var callback = MakeCallback(-100L, 42L, $"ai:add:{token}");
        await handler.HandleAsync(callback, CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "ai.suggestion-added-msg"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
