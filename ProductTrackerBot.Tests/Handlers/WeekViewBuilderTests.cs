namespace ProductTrackerBot.Tests.Handlers;

using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;

public class WeekViewBuilderTests
{
    private static ILocalizer KeyLocalizer()
    {
        var m = new Mock<ILocalizer>();
        m.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);
        return m.Object;
    }

    private static ILocalizer MonthLocalizer()
    {
        var m = new Mock<ILocalizer>();
        m.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.1")).Returns("Jan");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.2")).Returns("Feb");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.3")).Returns("Mar");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.4")).Returns("Apr");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.5")).Returns("May");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.6")).Returns("Jun");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.7")).Returns("Jul");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.8")).Returns("Aug");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.9")).Returns("Sep");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.10")).Returns("Oct");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.11")).Returns("Nov");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.month.12")).Returns("Dec");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.btn-prev-week")).Returns("← {0}");
        m.Setup(l => l.Get(It.IsAny<long>(), "week.btn-next-week")).Returns("{0} →");
        return m.Object;
    }

    // --- 7.1 GetWeekMonday ---

    [Fact]
    public void GetWeekMonday_Monday_Returns_Self()
    {
        var monday = new DateOnly(2026, 5, 18); // Monday
        Assert.Equal(monday, WeekViewBuilder.GetWeekMonday(monday));
    }

    [Fact]
    public void GetWeekMonday_Wednesday_Returns_That_Week_Monday()
    {
        var wednesday = new DateOnly(2026, 5, 20);
        var expected = new DateOnly(2026, 5, 18);
        Assert.Equal(expected, WeekViewBuilder.GetWeekMonday(wednesday));
    }

    [Fact]
    public void GetWeekMonday_Sunday_Returns_That_Week_Monday()
    {
        var sunday = new DateOnly(2026, 5, 24);
        var expected = new DateOnly(2026, 5, 18);
        Assert.Equal(expected, WeekViewBuilder.GetWeekMonday(sunday));
    }

    // --- 7.2 FormatWeekRange ---

    [Fact]
    public void FormatWeekRange_SameMonth_Formats_Correctly()
    {
        var monday = new DateOnly(2026, 5, 18);
        var result = WeekViewBuilder.FormatWeekRange(monday, MonthLocalizer(), -100L);
        Assert.Equal("May 18–24, 2026", result);
    }

    [Fact]
    public void FormatWeekRange_CrossMonth_Formats_Correctly()
    {
        var monday = new DateOnly(2026, 5, 29);
        var result = WeekViewBuilder.FormatWeekRange(monday, MonthLocalizer(), -100L);
        Assert.Equal("May 29 – Jun 4, 2026", result);
    }

    // --- 7.3 BuildDayListKeyboard ---

    [Fact]
    public void BuildDayListKeyboard_Nav_Row_Is_Last()
    {
        var monday = new DateOnly(2026, 5, 18);
        var keyboard = WeekViewBuilder.BuildDayListKeyboard(MonthLocalizer(), -100L, monday);
        var rows = keyboard.InlineKeyboard.ToList();

        // 7 days in rows of 3 → rows: [3,3,1], then nav row → 4 rows total
        Assert.Equal(4, rows.Count);
        var navRow = rows.Last().ToList();
        Assert.Equal(2, navRow.Count);
    }

    [Fact]
    public void BuildDayListKeyboard_Prev_Button_Data_Is_Correct()
    {
        var monday = new DateOnly(2026, 5, 18);
        var keyboard = WeekViewBuilder.BuildDayListKeyboard(MonthLocalizer(), -100L, monday);
        var navRow = keyboard.InlineKeyboard.Last().ToList();
        Assert.Equal("week:nav:2026-05-11", navRow[0].CallbackData);
    }

    [Fact]
    public void BuildDayListKeyboard_Next_Button_Data_Is_Correct()
    {
        var monday = new DateOnly(2026, 5, 18);
        var keyboard = WeekViewBuilder.BuildDayListKeyboard(MonthLocalizer(), -100L, monday);
        var navRow = keyboard.InlineKeyboard.Last().ToList();
        Assert.Equal("week:nav:2026-05-25", navRow[1].CallbackData);
    }

    [Fact]
    public void BuildDayListKeyboard_Day_Button_Data_Includes_WeekStart()
    {
        var monday = new DateOnly(2026, 5, 18);
        var keyboard = WeekViewBuilder.BuildDayListKeyboard(MonthLocalizer(), -100L, monday);
        var allButtons = keyboard.InlineKeyboard.SelectMany(r => r).ToList();
        var dayButtons = allButtons.Where(b => b.CallbackData?.StartsWith("week:day:") == true).ToList();

        Assert.Equal(7, dayButtons.Count);
        Assert.All(dayButtons, b => Assert.Contains("2026-05-18", b.CallbackData));
        Assert.Equal("week:day:2026-05-18:1", dayButtons[0].CallbackData);
        Assert.Equal("week:day:2026-05-18:7", dayButtons[6].CallbackData);
    }

    // --- 7.4 BuildDayDetailKeyboard ---

    [Fact]
    public void BuildDayDetailKeyboard_All_Callbacks_Contain_WeekStartDate()
    {
        const string weekStart = "2026-05-18";
        var dayMeals = new List<DayMealEntry>
        {
            new() { DayOfWeek = 1, MealId = 5, MealName = "Pasta" },
        };
        var keyboard = WeekViewBuilder.BuildDayDetailKeyboard(1, dayMeals, hasMeals: true, KeyLocalizer(), -100L, weekStart);

        var allCallbacks = keyboard.InlineKeyboard
            .SelectMany(r => r)
            .Select(b => b.CallbackData)
            .Where(d => d != "week:noop")
            .ToList();

        Assert.All(allCallbacks, cb => Assert.Contains(weekStart, cb));
    }

    // --- 7.5 BuildWeekSummaryText ---

    [Fact]
    public void BuildWeekSummaryText_Header_Includes_Week_Range()
    {
        var monday = new DateOnly(2026, 5, 18);
        var loc = new Mock<ILocalizer>();
        loc.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);
        loc.Setup(l => l.Get(It.IsAny<long>(), "week.header")).Returns("📅 Meal plan:");
        loc.Setup(l => l.Get(It.IsAny<long>(), "week.month.5")).Returns("May");

        var result = WeekViewBuilder.BuildWeekSummaryText(new List<DayMealEntry>(), loc.Object, -100L, monday);

        Assert.StartsWith("📅 Meal plan: May 18–24, 2026", result);
    }
}
