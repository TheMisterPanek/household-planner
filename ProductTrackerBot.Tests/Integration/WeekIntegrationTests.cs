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
    public async Task Week_In_Group_Sends_Header()
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
    public async Task Week_Assign_Persists_In_Repository()
    {
        await ClearDataAsync();

        // Create a meal first
        var group = await GroupRepository.GetOrCreateAsync(-100);
        var meal = await MealRepository.AddAsync(group.Id, "Pasta");

        // Assign meal to Monday (day 1)
        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"week:assign:1:{meal.Id}"));

        var plan = await DayMealsRepository.GetWeekAsync(group.Id);
        Assert.Single(plan);
        Assert.Equal(1, plan[0].DayOfWeek);
        Assert.Equal("Pasta", plan[0].MealName);
    }

    [Fact]
    public async Task Week_Clear_Removes_From_Repository()
    {
        await ClearDataAsync();

        // Create a meal and assign it
        var group = await GroupRepository.GetOrCreateAsync(-100);
        var meal = await MealRepository.AddAsync(group.Id, "Pasta");
        await DayMealsRepository.UpsertAsync(group.Id, 3, meal.Id);

        // Clear Wednesday (day 3)
        await DispatchAsync(CallbackUpdate(-100, 42, 99, "week:clear:3"));

        var plan = await DayMealsRepository.GetWeekAsync(group.Id);
        Assert.Empty(plan);
    }

    [Fact]
    public async Task Week_Pick_With_No_Meals_Answers_Callback()
    {
        await ClearDataAsync();

        // Ensure group exists
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

        // Create a meal
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
    public async Task Week_Back_Renders_Week_View()
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
}
