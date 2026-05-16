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

public class BuyCancelCallbackHandlerTests
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
    public async Task Cancel_ClearsSession_SendsCancelledMessage()
    {
        var bot = CreateBotMock();
        var pendingAddService = new PendingAddService();
        var token = pendingAddService.Store(new PendingAddItem(
            ChatId: -100L,
            GroupId: 10,
            Name: "Молоко",
            Quantity: null,
            AddedByName: "Alice"));

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.cancelled"))
            .Returns("Cancelled.");

        var handler = new BuyCancelCallbackHandler(bot.Object, pendingAddService, localizer.Object);

        var cbQuery = DeserializeCallbackQuery(
            $"{{\"id\":\"cb1\",\"from\":{{\"id\":42,\"first_name\":\"Alice\"}}," +
            $"\"message\":{{\"message_id\":5,\"chat\":{{\"id\":-100}},\"text\":\"test\"}}," +
            $"\"data\":\"buy:cancel:{token}\"}}");

        await handler.HandleAsync(cbQuery, CancellationToken.None);

        // Session cleared
        Assert.Null(pendingAddService.Get(token));

        // Cancelled message sent
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_WithAlreadyExpiredToken_StillSendsCancelledMessage()
    {
        var bot = CreateBotMock();
        var pendingAddService = new PendingAddService();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyCancelCallbackHandler(bot.Object, pendingAddService, localizer.Object);

        var cbQuery = DeserializeCallbackQuery(
            "{\"id\":\"cb2\",\"from\":{\"id\":42,\"first_name\":\"Alice\"}," +
            "\"message\":{\"message_id\":5,\"chat\":{\"id\":-100},\"text\":\"test\"}," +
            "\"data\":\"buy:cancel:deadbeef\"}");

        await handler.HandleAsync(cbQuery, CancellationToken.None);

        // Even with unknown token, cancelled message is sent (no-op clear)
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
