namespace ProductTrackerBot.Tests.Handlers;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class WeekCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message CreateGroupMessage(string text, long chatId = -100L)
    {
        var json = JsonSerializer.Serialize(new
        {
            message_id = 1,
            from = new { id = 456, first_name = "Test" },
            chat = new { id = chatId, type = "group" },
            text,
        }, JsonOpts);
        return JsonSerializer.Deserialize<Message>(json, JsonOpts)!;
    }

    private static Message CreatePrivateMessage(string text, long chatId = 456L)
    {
        var json = JsonSerializer.Serialize(new
        {
            message_id = 1,
            from = new { id = 456, first_name = "Test" },
            chat = new { id = chatId, type = "private" },
            text,
        }, JsonOpts);
        return JsonSerializer.Deserialize<Message>(json, JsonOpts)!;
    }

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

    private static WeekCommandHandler CreateHandler(
        Mock<ITelegramBotClient> bot,
        GroupRepository? groupRepo = null,
        DayMealsRepository? dayMealsRepo = null,
        ILocalizer? localizer = null)
    {
        var gr = groupRepo ?? new Mock<GroupRepository>(MockBehavior.Default, "cs").Object;
        var dr = dayMealsRepo ?? new Mock<DayMealsRepository>("cs").Object;
        var loc = localizer ?? Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), It.IsAny<string>()) == "key");

        return new WeekCommandHandler(
            bot.Object, gr, dr, loc,
            Mock.Of<ILogger<WeekCommandHandler>>());
    }

    [Fact]
    public async Task HandleAsync_Replies_GroupOnly_In_Private_Chat()
    {
        var (bot, sentTexts) = CreateBotMock();
        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "common.group-only") == "Group only!");

        var handler = CreateHandler(bot, localizer: localizer);
        await handler.HandleAsync(CreatePrivateMessage("/week"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Equal("Group only!", sentTexts[0]);
    }

    [Fact]
    public async Task HandleAsync_Sends_Week_Summary_With_Day_Buttons()
    {
        var (bot, sentTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Models.Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1)).ReturnsAsync(new List<Models.DayMealEntry>());

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.header") == "Plan:" &&
            l.Get(It.IsAny<long>(), "week.day.1") == "Mon" &&
            l.Get(It.IsAny<long>(), "week.day.2") == "Tue" &&
            l.Get(It.IsAny<long>(), "week.day.3") == "Wed" &&
            l.Get(It.IsAny<long>(), "week.day.4") == "Thu" &&
            l.Get(It.IsAny<long>(), "week.day.5") == "Fri" &&
            l.Get(It.IsAny<long>(), "week.day.6") == "Sat" &&
            l.Get(It.IsAny<long>(), "week.day.7") == "Sun");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, localizer);
        await handler.HandleAsync(CreateGroupMessage("/week"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.StartsWith("Plan:", sentTexts[0]);
        Assert.Contains("Mon: —", sentTexts[0]);
        Assert.Contains("Sun: —", sentTexts[0]);
    }
}
