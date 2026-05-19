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

    // 8.1: /week sends keyboard with 7 day buttons and a nav row
    [Fact]
    public async Task HandleAsync_Sends_Keyboard_With_7_Day_Buttons_And_Nav_Row()
    {
        var (bot, _) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Models.Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1, It.IsAny<string>())).ReturnsAsync(new List<Models.DayMealEntry>());

        SendMessageRequest? captured = null;
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) => { if (req is SendMessageRequest smr) captured = smr; })
            .ReturnsAsync(new Message());

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object);
        await handler.HandleAsync(CreateGroupMessage("/week"), CancellationToken.None);

        Assert.NotNull(captured);
        var inlineMarkup = (Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)captured!.ReplyMarkup!;
        var rows = inlineMarkup.InlineKeyboard.ToList();
        // 3 + 3 + 1 day rows + 1 nav row = 4 rows
        Assert.Equal(4, rows.Count);
        var dayButtons = rows.Take(3).SelectMany(r => r).ToList();
        Assert.Equal(7, dayButtons.Count);
        var navRow = rows.Last().ToList();
        Assert.Equal(2, navRow.Count);
    }

    // 8.2: GetWeekAsync is called with current week's Monday date string
    [Fact]
    public async Task HandleAsync_Calls_GetWeekAsync_With_Current_Monday()
    {
        var (bot, _) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Models.Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1, It.IsAny<string>())).ReturnsAsync(new List<Models.DayMealEntry>());

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object);
        await handler.HandleAsync(CreateGroupMessage("/week"), CancellationToken.None);

        var expectedMonday = WeekViewBuilder.GetWeekMonday(DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");
        dayMealsRepo.Verify(r => r.GetWeekAsync(1, expectedMonday), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Sends_Week_Summary_With_Day_Names()
    {
        var (bot, sentTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Models.Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1, It.IsAny<string>())).ReturnsAsync(new List<Models.DayMealEntry>());

        var locMock = new Mock<ILocalizer>();
        locMock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);
        locMock.Setup(l => l.Get(It.IsAny<long>(), "week.header")).Returns("Plan:");
        locMock.Setup(l => l.Get(It.IsAny<long>(), "week.day.1")).Returns("Mon");
        locMock.Setup(l => l.Get(It.IsAny<long>(), "week.day.7")).Returns("Sun");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, locMock.Object);
        await handler.HandleAsync(CreateGroupMessage("/week"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.StartsWith("Plan:", sentTexts[0]);
        Assert.Contains("Mon: —", sentTexts[0]);
        Assert.Contains("Sun: —", sentTexts[0]);
    }
}
