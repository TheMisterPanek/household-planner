using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class ListPaginationIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task ListNext_Sends_Or_Edits_List_Message()
    {
        await ClearDataAsync();

        // Seed 11 items to trigger pagination (page size = 10)
        var group = await GroupRepository.GetOrCreateAsync(-100);
        for (var i = 1; i <= 11; i++)
        {
            await ItemRepository.AddAsync(group.Id, $"Item{i}", null, "TestUser");
        }

        BotMock.Invocations.Clear();

        // Navigate to page 2
        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"list_next:-100:2"));

        BotMock.Verify(
            b => b.SendRequest(
                It.IsAny<EditMessageTextRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListPrev_Sends_Or_Edits_List_Message()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        for (var i = 1; i <= 11; i++)
        {
            await ItemRepository.AddAsync(group.Id, $"Item{i}", null, "TestUser");
        }

        BotMock.Invocations.Clear();

        // Navigate back to page 1 from page 2
        await DispatchAsync(CallbackUpdate(-100, 42, 99, $"list_prev:-100:1"));

        BotMock.Verify(
            b => b.SendRequest(
                It.IsAny<EditMessageTextRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
