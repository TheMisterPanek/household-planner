using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class MealIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task Meals_Command_In_Group_Sends_Meals_List()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/meals"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MealNew_Callback_Starts_Meal_Creation_Dialog()
    {
        await ClearDataAsync();

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "meal:new"));

        // Should send a prompt asking for meal name
        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => !string.IsNullOrEmpty(r.Text)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MealCreate_Dialog_Persists_Meal_In_Repository()
    {
        await ClearDataAsync();

        // Start meal creation via callback
        await DispatchAsync(CallbackUpdate(-100, 42, 99, "meal:new"));

        // Enter meal name
        await DispatchAsync(MessageUpdate(-100, 42, "Pasta"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var meals = await MealRepository.GetAllAsync(group.Id);
        Assert.Contains(meals, m => m.Name == "Pasta");
    }
}
