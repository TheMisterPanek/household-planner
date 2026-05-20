using Microsoft.Data.Sqlite;
using Moq;
using ProductTrackerBot.Models;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class BoughtCommandIntegrationTests : TelegramIntegrationTestBase
{
    public BoughtCommandIntegrationTests()
    {
    }

    [Fact]
    public async Task BoughtWithInlineArgs_SendsExpiryPrompt_StateIsStep2()
    {
        await this.ClearDataAsync();

        await this.DispatchAsync(CommandUpdate(-100L, 42L, "/bought milk 1L"));

        this.BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("bought.expiry-prompt")),
            It.IsAny<CancellationToken>()), Times.Once);

        var state = this.BoughtDialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Equal("milk", state.ItemName);
        Assert.Equal("1L", state.Quantity);
    }

    [Fact]
    public async Task BoughtWithDaysExpiry_SavesRecord_WithExpDate()
    {
        await this.ClearDataAsync();

        // /bought milk 1L → state Step=2
        await this.DispatchAsync(CommandUpdate(-100L, 42L, "/bought milk 1L"));

        // Send "7 days"
        await this.DispatchAsync(MessageUpdate(-100L, 42L, "7 days"));

        // Verify purchase history contains the row
        await using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ItemName, Quantity, exp_date FROM PurchaseHistory WHERE ItemName='milk'";
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), "Expected a row in PurchaseHistory");
        Assert.Equal("milk", reader.GetString(0));
        Assert.Equal("1L", reader.GetString(1));
        var expDate = DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd");
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddDays(7), expDate);

        this.BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("bought.done-with-expiry")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        Assert.Null(this.BoughtDialogService.GetState(-100L, 42L));
    }

    [Fact]
    public async Task BoughtWithSkipExpiry_SavesRecord_NullExpDate_ClearsState()
    {
        await this.ClearDataAsync();

        // /bought eggs → state Step=2
        await this.DispatchAsync(CommandUpdate(-100L, 42L, "/bought eggs"));

        // Tap [Skip] callback
        await this.DispatchAsync(CallbackUpdate(-100L, 42L, 1, "bought:skip_expiry"));

        // Verify purchase history
        await using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ItemName, exp_date FROM PurchaseHistory WHERE ItemName='eggs'";
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), "Expected a row in PurchaseHistory");
        Assert.Equal("eggs", reader.GetString(0));
        Assert.True(reader.IsDBNull(1), "exp_date should be null");

        Assert.Null(this.BoughtDialogService.GetState(-100L, 42L));
    }

    [Fact]
    public async Task BoughtInPrivateChat_SendsGroupOnly_NoStateSet()
    {
        await this.ClearDataAsync();

        await this.DispatchAsync(PrivateCommandUpdate(42L, 42L, "/bought"));

        this.BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "common.group-only"),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Null(this.BoughtDialogService.GetState(42L, 42L));
    }
}
