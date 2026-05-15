using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class LanguageIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task Settings_Sends_Settings_Keyboard()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/settings"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => !string.IsNullOrEmpty(r.Text)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Language_Command_Sends_Language_Selection()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/language"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Lang_Callback_Updates_Group_Language()
    {
        await ClearDataAsync();

        // Create the group first so SetLanguageAsync has something to update
        await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CallbackUpdate(-100, 42, 99, "lang:en"));

        // Verify language was persisted
        var group = await GroupRepository.GetOrCreateAsync(-100);
        Assert.Equal("en", group.LanguageCode);
    }
}
