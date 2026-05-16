using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class LoginCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message MakeMessage(string text) =>
        JsonSerializer.Deserialize<Message>(
            $"{{\"message_id\":1,\"from\":{{\"id\":42,\"first_name\":\"Alice\"}},\"chat\":{{\"id\":100}},\"text\":\"{text}\"}}",
            JsonOpts)!;

    private static (LoginCommandHandler handler, Mock<ITelegramBotClient> botMock, LoginCodeStore store) CreateHandler()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var store = new LoginCodeStore(TimeProvider.System);
        var handler = new LoginCommandHandler(botMock.Object, localizer.Object, store);
        return (handler, botMock, store);
    }

    private static string? CaptureReply(Mock<ITelegramBotClient> botMock)
    {
        var call = botMock.Invocations
            .FirstOrDefault(i => i.Method.Name == "SendRequest");
        if (call?.Arguments[0] is SendMessageRequest req)
            return req.Text;
        return null;
    }

    [Fact]
    public async Task HandleAsync_NoCode_RepliesLoginUsage()
    {
        var (handler, botMock, _) = CreateHandler();

        await handler.HandleAsync(MakeMessage("/login"), CancellationToken.None);

        Assert.Equal("login.usage", CaptureReply(botMock));
    }

    [Fact]
    public async Task HandleAsync_ValidCode_RepliesLoginSuccess()
    {
        var (handler, botMock, store) = CreateHandler();
        var code = store.GenerateCode(out _);

        await handler.HandleAsync(MakeMessage($"/login {code}"), CancellationToken.None);

        Assert.Equal("login.success", CaptureReply(botMock));
    }

    [Fact]
    public async Task HandleAsync_InvalidCode_RepliesLoginInvalid()
    {
        var (handler, botMock, _) = CreateHandler();

        await handler.HandleAsync(MakeMessage("/login BADCODE"), CancellationToken.None);

        Assert.Equal("login.invalid", CaptureReply(botMock));
    }

    [Fact]
    public async Task HandleAsync_CodeWithWhitespace_IsTrimmedBeforeVerify()
    {
        var (handler, botMock, store) = CreateHandler();
        var code = store.GenerateCode(out _);

        // Extra spaces around the code — handler must trim before calling TryVerify
        await handler.HandleAsync(MakeMessage($"/login  {code} "), CancellationToken.None);

        Assert.Equal("login.success", CaptureReply(botMock));
    }
}
