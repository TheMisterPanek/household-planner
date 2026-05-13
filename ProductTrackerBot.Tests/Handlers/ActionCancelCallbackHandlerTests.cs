using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProductTrackerBot.Tests.Handlers;

public class ActionCancelCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery CancelCallback() =>
        JsonSerializer.Deserialize<CallbackQuery>(
            "{\"id\":\"cb1\",\"from\":{\"id\":42,\"first_name\":\"Alice\"}," +
            "\"message\":{\"message_id\":99,\"chat\":{\"id\":-100}}," +
            "\"data\":\"action:cancel\"}",
            JsonOpts)!;

    private static ActionCancelCallbackHandler CreateHandler(Mock<ITelegramBotClient> bot) =>
        new(bot.Object, Mock.Of<ILogger<ActionCancelCallbackHandler>>());

    [Fact]
    public void CallbackPrefix_Returns_Action_Cancel()
    {
        var handler = CreateHandler(new Mock<ITelegramBotClient>());
        Assert.Equal("action:cancel", handler.CallbackPrefix);
    }

    [Fact]
    public async Task Deletes_Message_On_Success()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(bot);
        await handler.HandleAsync(CancelCallback(), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.Is<DeleteMessageRequest>(r => r.ChatId == -100L && r.MessageId == 99), It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<EditMessageReplyMarkupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Falls_Back_To_Keyboard_Removal_When_Delete_Throws()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiRequestException("Bad Request: message can't be deleted", 400));
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageReplyMarkupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(bot);
        await handler.HandleAsync(CancelCallback(), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.Is<EditMessageReplyMarkupRequest>(r => r.ChatId == -100L && r.MessageId == 99 && r.ReplyMarkup == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Always_Answers_Callback_Query()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(bot);
        await handler.HandleAsync(CancelCallback(), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.Is<AnswerCallbackQueryRequest>(r => r.CallbackQueryId == "cb1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Also_Answers_Callback_Query_After_Fallback()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiRequestException("Bad Request", 400));
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageReplyMarkupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler(bot);
        await handler.HandleAsync(CancelCallback(), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Does_Nothing_When_Message_Is_Null()
    {
        var bot = new Mock<ITelegramBotClient>();
        var handler = CreateHandler(bot);

        var callbackQuery = JsonSerializer.Deserialize<CallbackQuery>(
            "{\"id\":\"cb1\",\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"data\":\"action:cancel\"}",
            JsonOpts)!;

        await handler.HandleAsync(callbackQuery, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
