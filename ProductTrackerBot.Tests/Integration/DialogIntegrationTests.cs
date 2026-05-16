using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class DialogIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task BuyDialog_Full_Flow_Persists_Item_In_Repository()
    {
        await ClearDataAsync();

        // Step 1: /buy (no args) starts dialog
        await DispatchAsync(CommandUpdate(-100, 42, "/buy"));
        // Step 2: item name entered → advances to step 2
        await DispatchAsync(MessageUpdate(-100, 42, "Milk"));
        // Step 3: quantity entered → sends review message
        await DispatchAsync(MessageUpdate(-100, 42, "1.99"));

        // Confirm the add from the review message
        var confirmData = GetLastBuyConfirmCallbackData();
        Assert.NotNull(confirmData);
        await DispatchAsync(CallbackUpdate(-100, 42, 1, confirmData));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Contains(items, i => i.Name == "Milk" && i.Quantity == "1.99");
    }

    [Fact]
    public async Task Dialog_Message_Ignored_When_No_Pending_State()
    {
        await ClearDataAsync();

        await DispatchAsync(MessageUpdate(-100, 42, "hello"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
