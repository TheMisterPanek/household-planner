using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class WeekIntegrationTests : TelegramIntegrationTestBase
{
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

    [Fact]
    public async Task Week_In_Group_Sends_Header_With_Day_Buttons()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/week"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text == "week.header"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Week_Day_View_Shows_Day_Header()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:day:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r => r.Text.Contains("week.day.1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Week_Assign_Persists_And_Shows_Day_Detail()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var meal = await MealRepository.AddAsync(group.Id, "Pasta");

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:assign:1:{meal.Id}"));

        var plan = await DayMealsRepository.GetWeekAsync(group.Id);
        Assert.Single(plan);
        Assert.Equal(1, plan[0].DayOfWeek);
        Assert.Equal("Pasta", plan[0].MealName);

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r => r.Text.Contains("week.day.1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Week_Clear_Removes_Specific_Meal()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var meal = await MealRepository.AddAsync(group.Id, "Pasta");
        await DayMealsRepository.InsertAsync(group.Id, 3, meal.Id);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:clear:3:{meal.Id}"));

        var plan = await DayMealsRepository.GetWeekAsync(group.Id);
        Assert.Empty(plan);
    }

    [Fact]
    public async Task Week_Pick_With_No_Meals_Answers_Callback()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:pick:1"));

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

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:pick:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r => r.Text == "week.pick-prompt"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Week_Back_Renders_Day_List()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:back"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r => r.Text == "week.header"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Week_ToCart_With_No_Meals_Answers_Callback()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:to_cart:1"));

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
        await DayMealsRepository.InsertAsync(group.Id, 1, meal.Id);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:to_cart:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<AnswerCallbackQueryRequest>(r => r.Text == "week.cart-added"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var cart = await ItemRepository.GetAllAsync(group.Id);
        Assert.Equal(2, cart.Count);
    }
}
