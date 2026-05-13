using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class UndoInlineCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery MakeQuery(long chatId = -100L, long userId = 42L) =>
        JsonSerializer.Deserialize<CallbackQuery>(
            $"{{\"id\":\"cq1\",\"from\":{{\"id\":{userId},\"first_name\":\"Alice\"}},\"message\":{{\"message_id\":7,\"chat\":{{\"id\":{chatId}}}}},\"data\":\"undo:inline\"}}",
            JsonOpts)!;

    private static (UndoInlineCallbackHandler Handler, Mock<IUndoService> UndoMock, Mock<ITelegramBotClient> Bot, PendingDialogService<PriceCaptureDialogState> DialogService)
        CreateHandler(string undoResult = "undo.success")
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageReplyMarkupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var undoMock = new Mock<IUndoService>();
        undoMock.Setup(s => s.UndoLastAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(undoResult);

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);

        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        var logger = Mock.Of<ILogger<UndoInlineCallbackHandler>>();

        var handler = new UndoInlineCallbackHandler(bot.Object, undoMock.Object, dialogService, localizer.Object, logger);
        return (handler, undoMock, bot, dialogService);
    }

    [Fact]
    public void CallbackPrefix_Is_Undo_Inline()
    {
        var (handler, _, _, _) = CreateHandler();
        Assert.Equal("undo:inline", handler.CallbackPrefix);
    }

    [Theory]
    [InlineData("undo.success")]
    [InlineData("undo.nothing")]
    [InlineData("undo.error")]
    public async Task Calls_UndoService_And_Sends_Localized_Reply(string resultKey)
    {
        var (handler, undoMock, bot, _) = CreateHandler(resultKey);
        var query = MakeQuery();

        await handler.HandleAsync(query, CancellationToken.None);

        undoMock.Verify(s => s.UndoLastAsync(-100L, 42L, It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(
            b => b.SendRequest(It.Is<SendMessageRequest>(r => r.Text == resultKey), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Clears_Price_Dialog_State_Before_Undo()
    {
        var (handler, _, _, dialogService) = CreateHandler();
        var query = MakeQuery(chatId: -100L, userId: 42L);

        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState { Step = 1, ItemName = "Milk" });
        Assert.NotNull(dialogService.GetState(-100L, 42L));

        await handler.HandleAsync(query, CancellationToken.None);

        Assert.Null(dialogService.GetState(-100L, 42L));
    }

    [Fact]
    public async Task Removes_Keyboard_After_Undo()
    {
        var (handler, _, bot, _) = CreateHandler();
        var query = MakeQuery();

        await handler.HandleAsync(query, CancellationToken.None);

        bot.Verify(
            b => b.SendRequest(It.IsAny<EditMessageReplyMarkupRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Returns_Gracefully_When_Message_Is_Null()
    {
        var (handler, undoMock, _, _) = CreateHandler();
        var query = JsonSerializer.Deserialize<CallbackQuery>(
            "{\"id\":\"cq2\",\"from\":{\"id\":1,\"first_name\":\"X\"},\"data\":\"undo:inline\"}",
            JsonOpts)!;

        await handler.HandleAsync(query, CancellationToken.None);

        undoMock.Verify(s => s.UndoLastAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
