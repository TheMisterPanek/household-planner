using ProductTrackerBot.Localization;
using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class HistoryCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Message GroupMessage(long chatId = -100L, long userId = 42L, string firstName = "Alice") =>
        DeserializeMessage($"{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"{firstName}\"}},\"chat\":{{\"id\":{chatId}}},\"text\":\"/history\"}}");

    private static Message PrivateMessage(long userId = 42L, string firstName = "Alice") =>
        DeserializeMessage($"{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"{firstName}\"}},\"chat\":{{\"id\":{userId}}},\"text\":\"/history\"}}");

    private static (Mock<ITelegramBotClient> Bot, List<string> SentTexts) CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var sentTexts = new List<string>();

        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sentTexts.Add(smr.Text);
            })
            .ReturnsAsync(new Message());

        return (botMock, sentTexts);
    }

    [Fact]
    public async Task Private_Chat_Sends_Group_Only_Message()
    {
        var (bot, sentTexts) = CreateBotMock();
        var historyMock = new Mock<IHistoryRepository>();
        var handler = new HistoryCommandHandler(bot.Object, historyMock.Object);

        await handler.HandleAsync(PrivateMessage(), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Equal("Эта команда работает только в групповом чате.", sentTexts[0]);
    }

    [Fact]
    public async Task Empty_History_Sends_Empty_Message()
    {
        var (bot, sentTexts) = CreateBotMock();
        var historyMock = new Mock<IHistoryRepository>();
        historyMock
            .Setup(h => h.GetRecentAsync(-100L, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BotActionEntry>());

        var handler = new HistoryCommandHandler(bot.Object, historyMock.Object);
        await handler.HandleAsync(GroupMessage(), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Equal("История пуста.", sentTexts[0]);
    }

    [Fact]
    public async Task Non_Empty_History_Sends_Formatted_Entries()
    {
        var (bot, sentTexts) = CreateBotMock();
        var historyMock = new Mock<IHistoryRepository>();

        var entries = new[]
        {
            new BotActionEntry
            {
                Id = 2,
                ChatId = -100L,
                UserId = 42L,
                UserName = "Alice",
                ActionType = BotActionType.ItemBought,
                Payload = "{}",
                RecordedAt = new DateTime(2026, 5, 8, 14, 30, 0, DateTimeKind.Utc),
            },
            new BotActionEntry
            {
                Id = 1,
                ChatId = -100L,
                UserId = 42L,
                UserName = "Alice",
                ActionType = BotActionType.ItemAdded,
                Payload = "{}",
                RecordedAt = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc),
            },
        };

        historyMock
            .Setup(h => h.GetRecentAsync(-100L, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        historyMock
            .Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new HistoryCommandHandler(bot.Object, historyMock.Object);
        await handler.HandleAsync(GroupMessage(), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Contains("14:30 08.05", sentTexts[0]);
        Assert.Contains("Alice", sentTexts[0]);
        Assert.Contains("купил(а)", sentTexts[0]);
        Assert.Contains("12:00 08.05", sentTexts[0]);
        Assert.Contains("добавил(а)", sentTexts[0]);
    }
}
