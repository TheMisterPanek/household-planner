using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests;

public class UpdateDispatcherTests
{
    private readonly Mock<ILogger<UpdateDispatcher>> loggerMock;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public UpdateDispatcherTests()
    {
        this.loggerMock = new Mock<ILogger<UpdateDispatcher>>();
    }

    [Fact]
    public async Task Routes_Known_Command()
    {
        var commandHandler = new Mock<ICommandHandler>();
        commandHandler.Setup(h => h.Command).Returns("/buy");
        var dispatcher = CreateDispatcher(new[] { commandHandler.Object }, Enumerable.Empty<ICallbackHandler>());

        var update = DeserializeUpdate("""
            {"update_id":1,"message":{"message_id":1,"from":{"id":100,"first_name":"Test"},"chat":{"id":1},"text":"/buy"}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        commandHandler.Verify(h => h.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Routes_Known_Command_With_Bot_Username()
    {
        var commandHandler = new Mock<ICommandHandler>();
        commandHandler.Setup(h => h.Command).Returns("/buy");
        var dispatcher = CreateDispatcher(new[] { commandHandler.Object }, Enumerable.Empty<ICallbackHandler>());

        var update = DeserializeUpdate("""
            {"update_id":1,"message":{"message_id":1,"from":{"id":100,"first_name":"Test"},"chat":{"id":1},"text":"/buy@MyTestBot"}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        commandHandler.Verify(h => h.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ignores_Unknown_Command()
    {
        var commandHandler = new Mock<ICommandHandler>();
        commandHandler.Setup(h => h.Command).Returns("/buy");
        var dispatcher = CreateDispatcher(new[] { commandHandler.Object }, Enumerable.Empty<ICallbackHandler>());

        var update = DeserializeUpdate("""
            {"update_id":1,"message":{"message_id":1,"from":{"id":100,"first_name":"Test"},"chat":{"id":1},"text":"/unknown"}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        commandHandler.Verify(h => h.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Routes_Known_Callback_Prefix()
    {
        var callbackHandler = new Mock<ICallbackHandler>();
        callbackHandler.Setup(h => h.CallbackPrefix).Returns("shop:done:");
        var dispatcher = CreateDispatcher(Enumerable.Empty<ICommandHandler>(), new[] { callbackHandler.Object });

        var update = DeserializeUpdate("""
            {"update_id":1,"callback_query":{"id":"cb1","from":{"id":100,"first_name":"Test"},"message":{"message_id":200,"chat":{"id":1}},"data":"shop:done:42"}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        callbackHandler.Verify(h => h.HandleAsync(It.IsAny<CallbackQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ignores_Unknown_Callback_Prefix()
    {
        var callbackHandler = new Mock<ICallbackHandler>();
        callbackHandler.Setup(h => h.CallbackPrefix).Returns("shop:done:");
        var dispatcher = CreateDispatcher(Enumerable.Empty<ICommandHandler>(), new[] { callbackHandler.Object });

        var update = DeserializeUpdate("""
            {"update_id":1,"callback_query":{"id":"cb1","from":{"id":100,"first_name":"Test"},"message":{"message_id":200,"chat":{"id":1}},"data":"unknown:something"}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        callbackHandler.Verify(h => h.HandleAsync(It.IsAny<CallbackQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handles_Error_Gracefully()
    {
        var commandHandler = new Mock<ICommandHandler>();
        commandHandler.Setup(h => h.Command).Returns("/buy");
        commandHandler
            .Setup(h => h.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));

        var dispatcher = CreateDispatcher(new[] { commandHandler.Object }, Enumerable.Empty<ICallbackHandler>());

        var update = DeserializeUpdate("""
            {"update_id":1,"message":{"message_id":1,"from":{"id":100,"first_name":"Test"},"chat":{"id":1},"text":"/buy"}}
            """);

        // Should not throw
        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        // Verify error was logged
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Ignores_Non_Text_Messages()
    {
        var commandHandler = new Mock<ICommandHandler>();
        commandHandler.Setup(h => h.Command).Returns("/buy");
        var dispatcher = CreateDispatcher(new[] { commandHandler.Object }, Enumerable.Empty<ICallbackHandler>());

        var update = DeserializeUpdate("""
            {"update_id":1,"message":{"message_id":1,"from":{"id":100,"first_name":"Test"},"chat":{"id":1}}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        commandHandler.Verify(h => h.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Mention_BotUsername_RoutesToAiHandler()
    {
        var aiHandler = new Mock<IAiQueryHandler>();
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<Telegram.Bot.Requests.GetMeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "mybot", IsBot = true, FirstName = "MyBot" });

        var identityService = new BotIdentityService(botMock.Object, Mock.Of<ILogger<BotIdentityService>>());
        await identityService.StartAsync(CancellationToken.None);

        var dispatcher = CreateDispatcher(
            Enumerable.Empty<ICommandHandler>(),
            Enumerable.Empty<ICallbackHandler>(),
            identityService,
            aiHandler.Object);

        // Message with entity type "mention" for @mybot
        var update = DeserializeUpdate("""
            {"update_id":1,"message":{"message_id":1,"from":{"id":100,"first_name":"Test"},"chat":{"id":1},
            "text":"@mybot what did we buy?",
            "entities":[{"type":"mention","offset":0,"length":6}]}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        aiHandler.Verify(h => h.HandleQueryAsync(
            It.IsAny<Message>(),
            It.Is<string>(q => q.Contains("what did we buy")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Mention_OtherUser_FallsThroughToDialogHandling()
    {
        var aiHandler = new Mock<IAiQueryHandler>();
        var dialogHandler = new Mock<IDialogMessageHandler>();
        dialogHandler.Setup(h => h.CanHandle(It.IsAny<long>(), It.IsAny<long>())).Returns(true);

        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<Telegram.Bot.Requests.GetMeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "mybot", IsBot = true, FirstName = "MyBot" });

        var identityService = new BotIdentityService(botMock.Object, Mock.Of<ILogger<BotIdentityService>>());
        await identityService.StartAsync(CancellationToken.None);

        var dispatcher = new UpdateDispatcher(
            Enumerable.Empty<ICommandHandler>(),
            Enumerable.Empty<ICallbackHandler>(),
            new[] { dialogHandler.Object },
            identityService,
            aiHandler.Object,
            this.loggerMock.Object);

        // Message mentioning @otheruser — NOT the bot
        var update = DeserializeUpdate("""
            {"update_id":1,"message":{"message_id":1,"from":{"id":100,"first_name":"Test"},"chat":{"id":1},
            "text":"@otheruser hello",
            "entities":[{"type":"mention","offset":0,"length":10}]}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        aiHandler.Verify(h => h.HandleQueryAsync(It.IsAny<Message>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        dialogHandler.Verify(h => h.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Mention_BotUsername_EmptyQuestion_RoutesToAiHandlerWithEmptyString()
    {
        var aiHandler = new Mock<IAiQueryHandler>();
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<Telegram.Bot.Requests.GetMeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "mybot", IsBot = true, FirstName = "MyBot" });

        var identityService = new BotIdentityService(botMock.Object, Mock.Of<ILogger<BotIdentityService>>());
        await identityService.StartAsync(CancellationToken.None);

        var dispatcher = CreateDispatcher(
            Enumerable.Empty<ICommandHandler>(),
            Enumerable.Empty<ICallbackHandler>(),
            identityService,
            aiHandler.Object);

        // Message is just the bot mention with no additional text
        var update = DeserializeUpdate("""
            {"update_id":1,"message":{"message_id":1,"from":{"id":100,"first_name":"Test"},"chat":{"id":1},
            "text":"@mybot",
            "entities":[{"type":"mention","offset":0,"length":6}]}}
            """);

        await dispatcher.HandleUpdateAsync(Mock.Of<ITelegramBotClient>(), update, CancellationToken.None);

        aiHandler.Verify(h => h.HandleQueryAsync(
            It.IsAny<Message>(),
            It.Is<string>(q => q == string.Empty),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Update DeserializeUpdate(string json)
    {
        return JsonSerializer.Deserialize<Update>(json, JsonOpts)!;
    }

    private UpdateDispatcher CreateDispatcher(
        IEnumerable<ICommandHandler> commandHandlers,
        IEnumerable<ICallbackHandler> callbackHandlers,
        BotIdentityService? botIdentityService = null,
        IAiQueryHandler? aiQueryHandler = null)
    {
        var botClientMock = new Mock<ITelegramBotClient>();
        botClientMock
            .Setup(b => b.SendRequest(It.IsAny<Telegram.Bot.Requests.GetMeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Telegram.Bot.Types.User { Id = 1, Username = "testbot", IsBot = true, FirstName = "Test" });
        var identityService = botIdentityService
            ?? new BotIdentityService(botClientMock.Object, Mock.Of<ILogger<BotIdentityService>>());
        return new UpdateDispatcher(
            commandHandlers,
            callbackHandlers,
            Enumerable.Empty<IDialogMessageHandler>(),
            identityService,
            aiQueryHandler ?? Mock.Of<IAiQueryHandler>(),
            this.loggerMock.Object);
    }
}