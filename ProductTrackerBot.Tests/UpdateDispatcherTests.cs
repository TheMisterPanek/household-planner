using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
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

    private static Update DeserializeUpdate(string json)
    {
        return JsonSerializer.Deserialize<Update>(json, JsonOpts)!;
    }

    private UpdateDispatcher CreateDispatcher(
        IEnumerable<ICommandHandler> commandHandlers,
        IEnumerable<ICallbackHandler> callbackHandlers)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(x => x.GetService(typeof(IEnumerable<ICommandHandler>))).Returns(commandHandlers);
        sp.Setup(x => x.GetService(typeof(IEnumerable<ICallbackHandler>))).Returns(callbackHandlers);
        sp.Setup(x => x.GetService(typeof(IEnumerable<IDialogMessageHandler>))).Returns(Enumerable.Empty<IDialogMessageHandler>());
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(sp.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);
        return new UpdateDispatcher(factory.Object, this.loggerMock.Object);
    }
}