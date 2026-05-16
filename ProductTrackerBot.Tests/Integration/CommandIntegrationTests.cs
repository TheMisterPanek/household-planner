using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class CommandIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task Start_Sends_Welcome_Message()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/start"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => !string.IsNullOrEmpty(r.Text)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Buy_With_Args_Sends_Review_Then_Adds_Item_On_Confirm()
    {
        await ClearDataAsync();

        // /buy with inline args sends a review message (no immediate DB add)
        await DispatchAsync(CommandUpdate(-100, 42, "/buy Milk 2l"));

        var confirmData = GetLastBuyConfirmCallbackData();
        Assert.NotNull(confirmData);

        // Item not added yet
        var group = await GroupRepository.GetOrCreateAsync(-100);
        Assert.Empty(await ItemRepository.GetAllAsync(group.Id));

        // Confirm the add
        BotMock.Invocations.Clear();
        await DispatchAsync(CallbackUpdate(-100, 42, 1, confirmData));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text.Contains("Milk")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Contains(items, i => i.Name == "Milk");
    }

    [Fact]
    public async Task Buy_Creates_Group_On_First_Use()
    {
        await ClearDataAsync();

        const long freshChatId = -88888L;
        await DispatchAsync(CommandUpdate(freshChatId, 42, "/buy FirstItem"));

        // Group is created on /buy even before confirm
        var group = await GroupRepository.GetOrCreateAsync(freshChatId);
        Assert.True(group.Id > 0);

        // Confirm to add the item
        var confirmData = GetLastBuyConfirmCallbackData();
        Assert.NotNull(confirmData);
        await DispatchAsync(CallbackUpdate(freshChatId, 42, 1, confirmData));

        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task List_Shows_Empty_List_For_New_Group()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/list"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task List_Shows_Items_After_Buy_And_Confirm()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Eggs"));
        var confirmData = GetLastBuyConfirmCallbackData();
        Assert.NotNull(confirmData);
        await DispatchAsync(CallbackUpdate(-100, 42, 1, confirmData));

        BotMock.Invocations.Clear();

        await DispatchAsync(CommandUpdate(-100, 42, "/list"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text.Contains("Eggs")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
