using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class BuySkipIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task BuySkipQuantity_Saves_Item_Without_Quantity()
    {
        await ClearDataAsync();

        // Start /buy dialog (step 1)
        await DispatchAsync(CommandUpdate(-100, 42, "/buy"));
        // Enter item name (advances to step 2)
        await DispatchAsync(MessageUpdate(-100, 42, "Rice"));
        BotMock.Invocations.Clear();

        // Skip the quantity step via callback
        await DispatchAsync(CallbackUpdate(-100, 42, 99, "buy:skip_quantity"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        var rice = Assert.Single(items, i => i.Name == "Rice");
        Assert.Null(rice.Quantity);

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Buy_With_Explicit_Quantity_Adds_Item_And_Quantity()
    {
        await ClearDataAsync();

        // Start dialog
        await DispatchAsync(CommandUpdate(-100, 42, "/buy"));
        // Enter name
        await DispatchAsync(MessageUpdate(-100, 42, "Sugar"));
        // Enter quantity (completes the dialog)
        await DispatchAsync(MessageUpdate(-100, 42, "1kg"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        var sugar = Assert.Single(items, i => i.Name == "Sugar");
        Assert.Equal("1kg", sugar.Quantity);
    }
}
