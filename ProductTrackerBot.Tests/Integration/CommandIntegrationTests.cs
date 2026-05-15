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
    public async Task Buy_With_Args_Adds_Item_And_Sends_Confirmation()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Milk 2l"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text.Contains("Milk")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Contains(items, i => i.Name == "Milk");
    }

    [Fact]
    public async Task Buy_Creates_Group_On_First_Use()
    {
        await ClearDataAsync();

        const long freshChatId = -88888L;
        await DispatchAsync(CommandUpdate(freshChatId, 42, "/buy FirstItem"));

        var group = await GroupRepository.GetOrCreateAsync(freshChatId);
        Assert.True(group.Id > 0);

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
    public async Task List_Shows_Items_After_Buy()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Eggs"));
        BotMock.Invocations.Clear();

        await DispatchAsync(CommandUpdate(-100, 42, "/list"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text.Contains("Eggs")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
