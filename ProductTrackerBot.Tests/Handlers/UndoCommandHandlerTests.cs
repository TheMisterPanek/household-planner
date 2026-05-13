using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class UndoCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message UndoMessage(long chatId = -100L, long userId = 42L) =>
        JsonSerializer.Deserialize<Message>(
            $"{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"Alice\"}},\"chat\":{{\"id\":{chatId}}},\"text\":\"/undo\"}}",
            JsonOpts)!;

    private static (UndoCommandHandler Handler, Mock<IUndoService> UndoMock, Mock<ITelegramBotClient> Bot) CreateHandler(string undoResult)
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var undoMock = new Mock<IUndoService>();
        undoMock.Setup(s => s.UndoLastAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(undoResult);

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);

        var handler = new UndoCommandHandler(bot.Object, undoMock.Object, localizer.Object);
        return (handler, undoMock, bot);
    }

    [Theory]
    [InlineData("undo.success")]
    [InlineData("undo.nothing")]
    [InlineData("undo.error")]
    public async Task Delegates_To_UndoService_And_Sends_Localized_Reply(string resultKey)
    {
        var (handler, undoMock, bot) = CreateHandler(resultKey);
        var message = UndoMessage();

        await handler.HandleAsync(message, CancellationToken.None);

        undoMock.Verify(s => s.UndoLastAsync(-100L, 42L, It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(
            b => b.SendRequest(It.Is<SendMessageRequest>(r => r.Text == resultKey), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Command_Is_Slash_Undo()
    {
        var (handler, _, _) = CreateHandler("undo.nothing");
        Assert.Equal("/undo", handler.Command);
    }

    [Fact]
    public void Description_Is_Not_Null()
    {
        var (handler, _, _) = CreateHandler("undo.nothing");
        Assert.NotNull(handler.Description);
    }
}
