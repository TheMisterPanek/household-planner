using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class HistoryCommandIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task History_In_Group_With_Actions_Sends_Formatted_History()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Milk 2l"));
        BotMock.Invocations.Clear();

        await DispatchAsync(CommandUpdate(-100, 42, "/history"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => !string.IsNullOrEmpty(r.Text)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task History_With_No_Actions_Sends_Empty_Message()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/history"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
