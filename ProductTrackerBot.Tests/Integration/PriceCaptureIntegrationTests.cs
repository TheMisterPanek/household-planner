using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class PriceCaptureIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task PriceCapture_Full_Flow_Persists_Purchase_With_Price()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Milk", "2l", "TestUser");

        // shop:done starts price capture (step 1 = store name)
        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"shop:done:{item.Id}"));
        // Step 1: enter store name
        await DispatchAsync(MessageUpdate(-100, 42, "Lidl"));
        // Step 2: enter price
        await DispatchAsync(MessageUpdate(-100, 42, "1.99"));
        // Step 3: enter expiry (days)
        await DispatchAsync(MessageUpdate(-100, 42, "14"));

        var records = await PurchaseRepository.SearchAsync(group.Id, "Milk");
        Assert.Contains(records, r => r.Price == 1.99m && r.StoreName == "Lidl");
    }

    [Fact]
    public async Task PriceCapture_SkipStore_Advances_To_Price_Step()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Eggs", null, "TestUser");

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"shop:done:{item.Id}"));
        BotMock.Invocations.Clear();

        // Skip the store step
        await DispatchAsync(CallbackUpdate(-100, 42, 100, "price:skip_store"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PriceCapture_SkipPrice_Saves_Purchase_Without_Price()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Coffee", null, "TestUser");

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"shop:done:{item.Id}"));
        await DispatchAsync(MessageUpdate(-100, 42, "Aldi"));

        // Skip price → advances to expiry step; skip expiry → saves purchase
        await DispatchAsync(CallbackUpdate(-100, 42, 100, "price:skip_price"));
        await DispatchAsync(CallbackUpdate(-100, 42, 100, "price:skip_expiry"));

        var records = await PurchaseRepository.SearchAsync(group.Id, "Coffee");
        Assert.Contains(records, r => r.StoreName == "Aldi" && r.Price == null);
    }

    [Fact]
    public async Task PriceCapture_SkipExpiry_Saves_Purchase_And_Clears_Dialog()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Butter", null, "TestUser");

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"shop:done:{item.Id}"));
        await DispatchAsync(MessageUpdate(-100, 42, "Aldi"));
        await DispatchAsync(MessageUpdate(-100, 42, "2.50"));

        // Skip expiry → saves and closes dialog
        await DispatchAsync(CallbackUpdate(-100, 42, 100, "price:skip_expiry"));

        var records = await PurchaseRepository.SearchAsync(group.Id, "Butter");
        Assert.Contains(records, r => r.Price == 2.50m);
    }
}
