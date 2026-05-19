using Moq;
using Telegram.Bot.Requests;
using ProductTrackerBot.Handlers;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class WeekIntegrationTests : TelegramIntegrationTestBase
{
    private static readonly string CurrentWeek =
        WeekViewBuilder.GetWeekMonday(DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Week_In_Private_Chat_Replies_GroupOnly()
    {
        await ClearDataAsync();

        await DispatchAsync(PrivateCommandUpdate(456, 42, "/week"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text == "common.group-only"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // 10.1: /week in group → message contains week range and keyboard has 9 buttons
    [Fact]
    public async Task Week_In_Group_Sends_Week_Summary_With_Day_Buttons_And_Nav()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        SendMessageRequest? captured = null;
        BotMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Telegram.Bot.Requests.Abstractions.IRequest<Telegram.Bot.Types.Message>, CancellationToken>(
                (req, _) => { if (req is SendMessageRequest smr) captured = smr; })
            .ReturnsAsync(new Telegram.Bot.Types.Message());

        await DispatchAsync(CommandUpdate(-100, 42, "/week"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r =>
                    r.Text.StartsWith("week.header") &&
                    r.Text.Contains("week.day.1")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(captured);
        var inlineMarkup = (Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)captured!.ReplyMarkup!;
        var allButtons = inlineMarkup.InlineKeyboard.SelectMany(r => r).ToList();
        // 7 day buttons + 2 nav buttons = 9
        Assert.Equal(9, allButtons.Count);
    }

    [Fact]
    public async Task Week_Day_View_Shows_Day_Header()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:day:{CurrentWeek}:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r => r.Text.Contains("week.day-header")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // 10.3: week:day:2026-05-18:2 → edits to day detail view
    [Fact]
    public async Task Week_Day_View_For_Tuesday_Shows_Correct_Day()
    {
        await ClearDataAsync();
        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:day:2026-05-18:2"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r => r.Text.Contains("week.day-header")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // 10.2: week:nav:2026-05-25 → edits message to show May 25 week
    [Fact]
    public async Task Week_Nav_Shows_Target_Week()
    {
        await ClearDataAsync();
        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:nav:2026-05-25"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r =>
                    r.Text.StartsWith("week.header") &&
                    r.Text.Contains("week.day.1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // 10.4: week:assign:2026-05-18:2:{mealId} → persists with WeekStartDate
    [Fact]
    public async Task Week_Assign_Persists_And_Shows_Day_Detail_With_Bullet_List()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var meal = await MealRepository.AddAsync(group.Id, "Pasta");

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:assign:{CurrentWeek}:1:{meal.Id}"));

        var plan = await DayMealsRepository.GetWeekAsync(group.Id, CurrentWeek);
        Assert.Single(plan);
        Assert.Equal(1, plan[0].DayOfWeek);
        Assert.Equal("Pasta", plan[0].MealName);

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r =>
                    r.Text.Contains("week.day-header") &&
                    r.Text.Contains("• Pasta")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // 10.5: week:clear:2026-05-18:2:{mealId} → removes scoped row
    [Fact]
    public async Task Week_Clear_Removes_Specific_Meal()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var meal = await MealRepository.AddAsync(group.Id, "Pasta");
        await DayMealsRepository.InsertAsync(group.Id, 3, meal.Id, CurrentWeek);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:clear:{CurrentWeek}:3:{meal.Id}"));

        var plan = await DayMealsRepository.GetWeekAsync(group.Id, CurrentWeek);
        Assert.Empty(plan);
    }

    [Fact]
    public async Task Week_Pick_With_No_Meals_Answers_Callback()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:pick:{CurrentWeek}:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<AnswerCallbackQueryRequest>(r => r.Text == "week.no-meals"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Week_Pick_With_Meals_Edits_Message()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        await MealRepository.AddAsync(group.Id, "Pasta");

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:pick:{CurrentWeek}:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r => r.Text == "week.pick-prompt"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // 10.6: week:back:2026-05-18 → returns to week list
    [Fact]
    public async Task Week_Back_Renders_Day_List()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:back:{CurrentWeek}"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r =>
                    r.Text.StartsWith("week.header") &&
                    r.Text.Contains("week.day.1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Week_ToCart_With_No_Meals_Answers_Callback()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:to_cart:{CurrentWeek}:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<AnswerCallbackQueryRequest>(r => r.Text == "week.cart-no-meals"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Week_ToCart_Adds_Ingredients()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var meal = await MealRepository.AddAsync(group.Id, "Pasta");
        await MealIngredientRepository.AddAsync(meal.Id, "Flour", "500g");
        await MealIngredientRepository.AddAsync(meal.Id, "Eggs", "3");
        await DayMealsRepository.InsertAsync(group.Id, 1, meal.Id, CurrentWeek);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:to_cart:{CurrentWeek}:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<AnswerCallbackQueryRequest>(r => r.Text == "week.cart-added"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var cart = await ItemRepository.GetAllAsync(group.Id);
        Assert.Equal(2, cart.Count);
    }
}
