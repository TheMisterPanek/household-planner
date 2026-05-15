using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class ActionCancelIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task ActionCancel_Deletes_Message()
    {
        await ClearDataAsync();

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "action:cancel"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
