using Microsoft.Data.Sqlite;
using Moq;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class SmartExpirySuggestionsIntegrationTests : TelegramIntegrationTestBase
{
    public SmartExpirySuggestionsIntegrationTests()
    {
    }

    private async Task SeedPurchaseWithExpiry(string itemName, int daysShelfLife)
    {
        await using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync();

        // Ensure group exists
        await using var groupCmd = conn.CreateCommand();
        groupCmd.CommandText = "INSERT OR IGNORE INTO Groups (ChatId) VALUES (-100)";
        await groupCmd.ExecuteNonQueryAsync();

        await using var idCmd = conn.CreateCommand();
        idCmd.CommandText = "SELECT Id FROM Groups WHERE ChatId = -100";
        var groupId = (long)(await idCmd.ExecuteScalarAsync())!;

        var purchasedAt = DateTime.UtcNow.AddDays(-daysShelfLife).ToString("O");
        var expDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PurchaseHistory (GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName, exp_date)
            VALUES (@g, 42, @name, NULL, NULL, NULL, @at, 'Alice', @exp)";
        cmd.Parameters.AddWithValue("@g", groupId);
        cmd.Parameters.AddWithValue("@name", itemName);
        cmd.Parameters.AddWithValue("@at", purchasedAt);
        cmd.Parameters.AddWithValue("@exp", expDate);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task BoughtFlow_SuggestionButtonsAppear_WhenSimilarHistoryExists()
    {
        await this.ClearDataAsync();
        // Seed two prior milk purchases with ~7-day shelf life
        await this.SeedPurchaseWithExpiry("milk", 7);
        await this.SeedPurchaseWithExpiry("milk 2L", 7);

        // Start /bought dialog then submit item name
        await this.DispatchAsync(CommandUpdate(-100L, 42L, "/bought"));
        await this.DispatchAsync(MessageUpdate(-100L, 42L, "milk"));

        var callbackData = this.GetLastExpirySuggestCallbackData();
        Assert.NotNull(callbackData);
        Assert.Equal("expiry:suggest:7", callbackData);
    }

    [Fact]
    public async Task BoughtFlow_TappingSuggestionButton_CompletesDialog_AndSavesExpiry()
    {
        await this.ClearDataAsync();
        await this.SeedPurchaseWithExpiry("milk", 7);

        await this.DispatchAsync(CommandUpdate(-100L, 42L, "/bought"));
        await this.DispatchAsync(MessageUpdate(-100L, 42L, "milk"));

        // Tap the suggestion button
        await this.DispatchAsync(CallbackUpdate(-100L, 42L, 5, "expiry:suggest:7"));

        // Assert confirmation was sent
        this.BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text.Contains("bought.done")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Assert PurchaseHistory has the row with expiry ~7 days from now
        await using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT exp_date FROM PurchaseHistory ORDER BY Id DESC LIMIT 1";
        var result = (string?)(await cmd.ExecuteScalarAsync());
        Assert.NotNull(result);
        var saved = DateOnly.ParseExact(result!, "yyyy-MM-dd");
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddDays(7), saved);
    }

    [Fact]
    public async Task BoughtFlow_FallbackAverageShown_WhenNoSimilarItemHistory()
    {
        await this.ClearDataAsync();
        // Seed unrelated items with 14-day shelf life (group average will be ~14)
        await this.SeedPurchaseWithExpiry("bread", 14);
        await this.SeedPurchaseWithExpiry("yogurt", 14);

        await this.DispatchAsync(CommandUpdate(-100L, 42L, "/bought"));
        await this.DispatchAsync(MessageUpdate(-100L, 42L, "newitem"));

        var callbackData = this.GetLastExpirySuggestCallbackData();
        Assert.NotNull(callbackData);
        Assert.Equal("expiry:suggest:14", callbackData);
    }

    [Fact]
    public async Task BoughtFlow_NoSuggestionButtons_WhenGroupHasNoHistory()
    {
        await this.ClearDataAsync();

        await this.DispatchAsync(CommandUpdate(-100L, 42L, "/bought"));
        await this.DispatchAsync(MessageUpdate(-100L, 42L, "milk"));

        var callbackData = this.GetLastExpirySuggestCallbackData();
        Assert.Null(callbackData);

        // Skip button is still present
        this.BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                (r.ReplyMarkup as InlineKeyboardMarkup) != null &&
                ((InlineKeyboardMarkup)r.ReplyMarkup!).InlineKeyboard
                    .Any(row => row.Any(btn => btn.CallbackData == "bought:skip_expiry"))),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
