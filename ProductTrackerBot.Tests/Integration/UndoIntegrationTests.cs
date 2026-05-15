using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class UndoIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task Undo_After_ShopDone_Restores_Item_To_Repository()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Milk", "2l", "TestUser");

        // Mark item as done — records ItemBought with revert payload
        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"shop:done:{item.Id}"));
        BotMock.Invocations.Clear();

        // /undo should restore the item
        await DispatchAsync(CommandUpdate(-100, 42, "/undo"));

        var itemsAfter = await ItemRepository.GetAllAsync(group.Id);
        Assert.Contains(itemsAfter, i => i.Name == "Milk");

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Undo_With_Nothing_To_Undo_Sends_Reply()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/undo"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UndoInline_After_ShopDone_Restores_Item()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Bread", null, "TestUser");

        // Mark item as done (removes from list, records history, starts price capture)
        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"shop:done:{item.Id}"));
        BotMock.Invocations.Clear();

        // Undo via inline button
        await DispatchAsync(CallbackUpdate(-100, 42, 100, "undo:inline"));

        // Item should be restored
        var itemsAfter = await ItemRepository.GetAllAsync(group.Id);
        Assert.Contains(itemsAfter, i => i.Name == "Bread");
    }
}
