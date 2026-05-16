using Moq;
using ProductTrackerBot.Models;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class AiSuggestionIntegrationTests : TelegramIntegrationTestBase
{
    // Task 11.1: /ai with suggestions → bot sends message with InlineKeyboardMarkup containing ai:add: buttons
    [Fact]
    public async Task AiCommand_WithSuggestions_SendsMessageWithInlineKeyboard()
    {
        await ClearDataAsync();

        AiQueryServiceMock
            .Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("Try pasta tonight!", new List<AiSuggestion>
            {
                new("pasta", "500g"),
                new("eggs", null),
            }));

        await DispatchAsync(CommandUpdate(-100, 42, "/ai suggest a recipe"));

        BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text == "Try pasta tonight!" &&
                r.ReplyMarkup is InlineKeyboardMarkup),
            It.IsAny<CancellationToken>()), Times.Once);

        var sentReq = BotMock.Invocations
            .Select(i => i.Arguments[0])
            .OfType<SendMessageRequest>()
            .First(r => r.Text == "Try pasta tonight!");
        var ikm = (InlineKeyboardMarkup)sentReq.ReplyMarkup!;
        var allButtons = ikm.InlineKeyboard.SelectMany(row => row).ToList();
        // All buttons use ai:add: or ai:add-all: prefix
        Assert.True(allButtons.All(b => b.CallbackData != null && b.CallbackData.StartsWith("ai:add")));
        Assert.Contains(allButtons, b => b.Text.Contains("pasta"));
        Assert.Contains(allButtons, b => b.Text.Contains("eggs"));
        // Add All button is present as the last row
        Assert.Contains(allButtons, b => b.CallbackData != null && b.CallbackData.StartsWith("ai:add-all:"));
    }

    // Task 11.2: ai:add:{token} callback → item added to repo, confirmation sent
    [Fact]
    public async Task AiAddCallback_ValidToken_AddsItemAndSendsConfirmation()
    {
        await ClearDataAsync();

        var token = AiSuggestionService.Store(new AiSuggestion("milk", "1L"));

        await DispatchAsync(CallbackUpdate(-100, 42, 1, $"ai:add:{token}"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Contains(items, i => i.Name == "milk");

        BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("ai.suggestion-added")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Task 11.3: ai:add:{invalid} callback → no item added, expiry message sent
    [Fact]
    public async Task AiAddCallback_InvalidToken_NoItemAdded_ExpiryMessageSent()
    {
        await ClearDataAsync();

        await DispatchAsync(CallbackUpdate(-100, 42, 1, "ai:add:00000000"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Empty(items);

        BotMock.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "ai.suggestion-expired"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ai:add-all:{token} → all items added to repository, summary confirmation sent
    [Fact]
    public async Task AiAddAllCallback_ValidToken_AddsAllItemsAndSendsConfirmation()
    {
        await ClearDataAsync();

        var suggestions = new List<ProductTrackerBot.Models.AiSuggestion>
        {
            new("milk", "1L"),
            new("bread", null),
        };
        var token = AiSuggestionService.StoreBatch(suggestions);

        await DispatchAsync(CallbackUpdate(-100, 42, 1, $"ai:add-all:{token}"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Contains(items, i => i.Name == "milk");
        Assert.Contains(items, i => i.Name == "bread");

        BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("ai.suggestion-all-added")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ai:add-all:{invalid} → no items added, expiry sent
    [Fact]
    public async Task AiAddAllCallback_InvalidToken_NoItemsAdded_ExpiryMessageSent()
    {
        await ClearDataAsync();

        await DispatchAsync(CallbackUpdate(-100, 42, 1, "ai:add-all:00000000"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Empty(items);

        BotMock.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "ai.suggestion-expired"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
