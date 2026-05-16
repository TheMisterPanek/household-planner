using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class BuyEditCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery DeserializeCallbackQuery(string json) =>
        JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return bot;
    }

    [Fact]
    public async Task ValidToken_SetsOneLinkModeDialogState_SendsEditPrompt_ClearsSession()
    {
        var bot = CreateBotMock();
        var pendingAddService = new PendingAddService();
        var dialogService = new PendingDialogService<BuyDialogState>();

        var token = pendingAddService.Store(new PendingAddItem(
            ChatId: -100L,
            GroupId: 10,
            Name: "отривин",
            Quantity: "1 шт",
            AddedByName: "Alice"));

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyEditCallbackHandler(bot.Object, pendingAddService, dialogService, localizer.Object);

        var cbQuery = DeserializeCallbackQuery(
            $"{{\"id\":\"cb1\",\"from\":{{\"id\":42,\"first_name\":\"Alice\"}}," +
            $"\"message\":{{\"message_id\":5,\"chat\":{{\"id\":-100}},\"text\":\"test\"}}," +
            $"\"data\":\"buy:edit:{token}\"}}");

        await handler.HandleAsync(cbQuery, CancellationToken.None);

        // Session cleared
        Assert.Null(pendingAddService.Get(token));

        // Dialog state set with IsOneLineMode = true
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.True(state!.IsOneLineMode);
        Assert.Equal(1, state.Step);
        Assert.Equal(10, state.GroupId);
        Assert.Equal("Alice", state.AddedByName);

        // Edit prompt sent
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpiredToken_AnswersWithError_DoesNotSetDialog()
    {
        var bot = CreateBotMock();
        var pendingAddService = new PendingAddService();
        var dialogService = new PendingDialogService<BuyDialogState>();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyEditCallbackHandler(bot.Object, pendingAddService, dialogService, localizer.Object);

        var cbQuery = DeserializeCallbackQuery(
            "{\"id\":\"cb2\",\"from\":{\"id\":42,\"first_name\":\"Alice\"}," +
            "\"message\":{\"message_id\":5,\"chat\":{\"id\":-100},\"text\":\"test\"}," +
            "\"data\":\"buy:edit:deadbeef\"}");

        await handler.HandleAsync(cbQuery, CancellationToken.None);

        Assert.Null(dialogService.GetState(-100L, 42L));
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
