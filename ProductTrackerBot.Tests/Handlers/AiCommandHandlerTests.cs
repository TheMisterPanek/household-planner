using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
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
        string localizationFallback = "{key}")
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var groupRepo = new Mock<GroupRepository>("Data Source=:memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync(new ProductTrackerBot.Models.Group { Id = 1, ChatId = -100, LanguageCode = "en" });

        var serviceMock = new Mock<IAiQueryService>();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new AiCommandHandler(
            botMock.Object,
            groupRepo.Object,
            serviceMock.Object,
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
        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NonEmptyQuestion_DelegatesToService()
    {
        var (handler, botMock, serviceMock) = CreateHandler();
        serviceMock.Setup(s => s.AnswerAsync(-100L, 1L, "what did we buy?", It.IsAny<CancellationToken>()))
            .ReturnsAsync("You bought milk.");

        var message = AiMessage("/ai what did we buy?");

        await handler.HandleAsync(message, CancellationToken.None);

        serviceMock.Verify(s => s.AnswerAsync(-100L, 1L, "what did we buy?", It.IsAny<CancellationToken>()), Times.Once);
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "You bought milk."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleQueryAsync_WithEmptyQuestion_SendsUsageHint()
    {
        var (handler, botMock, serviceMock) = CreateHandler();
        var message = AiMessage("/ai");

        await handler.HandleQueryAsync(message, string.Empty, CancellationToken.None);

        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "ai.usage-hint"),
            It.IsAny<CancellationToken>()), Times.Once);
        serviceMock.Verify(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleQueryAsync_WithQuestion_DelegatesToService()
    {
        var (handler, botMock, serviceMock) = CreateHandler();
        serviceMock.Setup(s => s.AnswerAsync(-100L, 1L, "my question", It.IsAny<CancellationToken>()))
            .ReturnsAsync("my answer");

        var message = AiMessage("@testbot my question");

        await handler.HandleQueryAsync(message, "my question", CancellationToken.None);

        serviceMock.Verify(s => s.AnswerAsync(-100L, 1L, "my question", It.IsAny<CancellationToken>()), Times.Once);
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "my answer"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
