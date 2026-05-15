using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class CallbackIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task ShopDone_Marks_Item_Done_And_Sends_Reply()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Milk", "2l", "TestUser");

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"shop:done:{item.Id}"));

        var remaining = await ItemRepository.GetAllAsync(group.Id);
        Assert.DoesNotContain(remaining, i => i.Id == item.Id);

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ShopRemove_Removes_Item_And_Sends_Reply()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Bread", null, "TestUser");

        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"shop:remove:{item.Id}"));

        var remaining = await ItemRepository.GetAllAsync(group.Id);
        Assert.DoesNotContain(remaining, i => i.Id == item.Id);
    }

    [Fact]
    public async Task Unknown_Callback_Prefix_No_Handler_Called()
    {
        await ClearDataAsync();

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "unknown:action:1"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
