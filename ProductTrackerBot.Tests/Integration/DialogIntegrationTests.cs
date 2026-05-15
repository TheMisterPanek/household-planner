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
        // Step 2: item name entered
        await DispatchAsync(MessageUpdate(-100, 42, "Milk"));
        // Step 3: quantity entered — BuyStepHandler finishes and adds the item
        await DispatchAsync(MessageUpdate(-100, 42, "1.99"));

        // Bot should have sent messages for both dialog steps (step 2 ask-quantity, step 3 confirmation)
        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));

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
