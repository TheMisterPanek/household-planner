using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class LoginIntegrationTests : TelegramIntegrationTestBase
{
    private string? LastReply()
    {
        var call = this.BotMock.Invocations
            .LastOrDefault(i => i.Method.Name == "SendRequest" && i.Arguments[0] is SendMessageRequest);
        return call?.Arguments[0] is SendMessageRequest req ? req.Text : null;
    }

    [Fact]
    public async Task Login_NoCode_RepliesLoginUsage()
    {
        await this.ClearDataAsync();

        await this.DispatchAsync(CommandUpdate(chatId: 100, userId: 1, text: "/login"));

        Assert.Equal("login.usage", this.LastReply());
    }

    [Fact]
    public async Task Login_BadCode_RepliesLoginInvalid()
    {
        await this.ClearDataAsync();

        await this.DispatchAsync(CommandUpdate(chatId: 100, userId: 1, text: "/login BADCODE"));

        Assert.Equal("login.invalid", this.LastReply());
    }

    [Fact]
    public async Task Login_ValidCode_RepliesLoginSuccess_AndSessionAvailable()
    {
        await this.ClearDataAsync();

        var code = this.LoginCodeStore.GenerateCode(out var token);
        await this.DispatchAsync(CommandUpdate(chatId: 100, userId: 1, text: $"/login {code}"));

        Assert.Equal("login.success", this.LastReply());
        Assert.True(this.LoginCodeStore.TryGetSession(token, out var chatId));
        Assert.Equal(100L, chatId);
    }
}
