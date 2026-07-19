using Moq;
using ProductTrackerBot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class NoOpCallbackHandlerTests
{
    [Fact]
    public void CallbackPrefix_IsNoop()
    {
        var handler = new NoOpCallbackHandler(Mock.Of<ITelegramBotClient>());

        Assert.Equal("noop", handler.CallbackPrefix);
    }

    [Fact]
    public async Task HandleAsync_AnswersCallback_AndTakesNoOtherAction()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock
            .Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new NoOpCallbackHandler(botMock.Object);
        var callbackQuery = new CallbackQuery { Id = "cb1" };

        await handler.HandleAsync(callbackQuery, CancellationToken.None);

        botMock.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        botMock.VerifyNoOtherCalls();
    }
}
