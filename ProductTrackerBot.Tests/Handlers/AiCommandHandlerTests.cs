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

public class AiCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Message AiMessage(string text) =>
        DeserializeMessage($"{{\"message_id\":1,\"from\":{{\"id\":42,\"first_name\":\"Alice\"}},\"chat\":{{\"id\":-100}},\"text\":\"{text}\"}}");

    private static (AiCommandHandler handler, Mock<ITelegramBotClient> botMock, Mock<IAiQueryService> serviceMock) CreateHandler(
        string localizationFallback = "{key}", string? languageCode = "en")
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var groupRepo = new Mock<GroupRepository>("Data Source=:memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync(new ProductTrackerBot.Models.Group { Id = 1, ChatId = -100, LanguageCode = languageCode! });

        var serviceMock = new Mock<IAiQueryService>();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new AiCommandHandler(
            botMock.Object,
            groupRepo.Object,
            serviceMock.Object,
            new ProductTrackerBot.Services.AiSuggestionService(),
            new ProductTrackerBot.Services.ConversationHistoryService(),
            localizer.Object,
            Mock.Of<ILogger<AiCommandHandler>>());

        return (handler, botMock, serviceMock);
    }

    [Fact]
    public async Task HandleAsync_EmptyQuestion_SendsUsageHint()
    {
        var (handler, botMock, serviceMock) = CreateHandler();
        var message = AiMessage("/ai");

        await handler.HandleAsync(message, CancellationToken.None);

        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "ai.usage-hint"),
            It.IsAny<CancellationToken>()), Times.Once);
        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EmptyQuestionWithSpaces_SendsUsageHint()
    {
        var (handler, botMock, serviceMock) = CreateHandler();
        var message = AiMessage("/ai   ");

        await handler.HandleAsync(message, CancellationToken.None);

        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "ai.usage-hint"),
            It.IsAny<CancellationToken>()), Times.Once);
        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NonEmptyQuestion_DelegatesToService()
    {
        var (handler, botMock, serviceMock) = CreateHandler();
        serviceMock.Setup(s => s.AnswerAsync(-100L, 1L, "what did we buy?", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("You bought milk.", []));

        var message = AiMessage("/ai what did we buy?");

        await handler.HandleAsync(message, CancellationToken.None);

        serviceMock.Verify(s => s.AnswerAsync(-100L, 1L, "what did we buy?", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "You bought milk."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Task 9.1: With suggestions → SendMessage includes InlineKeyboardMarkup with correct buttons
    [Fact]
    public async Task HandleAsync_WithSuggestions_SendsMessageWithInlineKeyboard()
    {
        var (handler, botMock, serviceMock) = CreateHandler();
        serviceMock.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("Try pasta carbonara!", new List<AiSuggestion>
            {
                new("pasta", "500g"),
                new("eggs", null),
            }));

        var message = AiMessage("/ai suggest a recipe");
        await handler.HandleAsync(message, CancellationToken.None);

        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text == "Try pasta carbonara!" &&
                r.ReplyMarkup is Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup),
            It.IsAny<CancellationToken>()), Times.Once);

        var sentMsg = (SendMessageRequest)botMock.Invocations
            .Where(i => i.Method.Name == "SendRequest" && i.Arguments[0] is SendMessageRequest smr && smr.Text == "Try pasta carbonara!")
            .Select(i => i.Arguments[0])
            .First();
        var keyboard = (Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)((SendMessageRequest)sentMsg).ReplyMarkup!;
        var rows = keyboard.InlineKeyboard.ToList();
        // 2 suggestion rows + 1 "Add All" row
        Assert.Equal(3, rows.Count);
        Assert.Contains("pasta", rows[0].First().Text);
        Assert.StartsWith("ai:add:", rows[0].First().CallbackData!);
        Assert.Contains("eggs", rows[1].First().Text);
        Assert.StartsWith("ai:add-all:", rows[2].First().CallbackData!);
    }

    // Task 9.2: Without suggestions → SendMessage has no replyMarkup
    [Fact]
    public async Task HandleAsync_NoSuggestions_SendsMessageWithoutInlineKeyboard()
    {
        var (handler, botMock, serviceMock) = CreateHandler();
        serviceMock.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("You have 5 items.", []));

        var message = AiMessage("/ai how many items?");
        await handler.HandleAsync(message, CancellationToken.None);

        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text == "You have 5 items." &&
                r.ReplyMarkup == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GroupLanguageEn_CallsAnswerAsyncWithEnglish()
    {
        var (handler, _, serviceMock) = CreateHandler(languageCode: "en");
        serviceMock.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("answer", []));

        await handler.HandleAsync(AiMessage("/ai test"), CancellationToken.None);

        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), "English", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GroupLanguageRu_CallsAnswerAsyncWithRussian()
    {
        var (handler, _, serviceMock) = CreateHandler(languageCode: "ru");
        serviceMock.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("answer", []));

        await handler.HandleAsync(AiMessage("/ai test"), CancellationToken.None);

        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), "Russian", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GroupLanguagePl_CallsAnswerAsyncWithPolish()
    {
        var (handler, _, serviceMock) = CreateHandler(languageCode: "pl");
        serviceMock.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("answer", []));

        await handler.HandleAsync(AiMessage("/ai test"), CancellationToken.None);

        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), "Polish", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GroupLanguageNull_CallsAnswerAsyncWithEnglishFallback()
    {
        var (handler, _, serviceMock) = CreateHandler(languageCode: null);
        serviceMock.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("answer", []));

        await handler.HandleAsync(AiMessage("/ai test"), CancellationToken.None);

        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), "English", It.IsAny<CancellationToken>()), Times.Once);
    }

}
