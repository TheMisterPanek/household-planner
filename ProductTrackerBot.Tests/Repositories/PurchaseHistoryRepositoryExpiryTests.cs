using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class PurchaseHistoryRepositoryExpiryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly PurchaseHistoryRepository repository;

    public PurchaseHistoryRepositoryExpiryTests()
    {
        this.connection = new SqliteConnection("Data Source=file:purchaseexpirytest?mode=memory&cache=shared");
        this.connection.Open();

        using var createGroup = this.connection.CreateCommand();
        createGroup.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER,
                LanguageCode TEXT NOT NULL DEFAULT 'ru'
            );";
        createGroup.ExecuteNonQuery();

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS PurchaseHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                UserId INTEGER NOT NULL,
                ItemName TEXT NOT NULL,
                Quantity TEXT,
                StoreName TEXT,
                Price REAL,
                PurchasedAt TEXT NOT NULL,
                BoughtByName TEXT NOT NULL,
                exp_date TEXT NULL
            );";
        cmd.ExecuteNonQuery();

        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM PurchaseHistory;
            DELETE FROM sqlite_sequence WHERE name = 'PurchaseHistory';
            DELETE FROM Groups;
            DELETE FROM sqlite_sequence WHERE name = 'Groups';";
        cleanCmd.ExecuteNonQuery();

        using var seedGroup = this.connection.CreateCommand();
        seedGroup.CommandText = "INSERT INTO Groups (ChatId) VALUES (-100)";
        seedGroup.ExecuteNonQuery();

        this.repository = new PurchaseHistoryRepository("Data Source=file:purchaseexpirytest?mode=memory&cache=shared");
    }

    public void Dispose() => this.connection.Dispose();

    private void InsertPurchase(int groupId, string itemName, string purchasedAt, string? expDate)
    {
        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PurchaseHistory (GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName, exp_date)
            VALUES (@g, 1, @name, NULL, NULL, NULL, @at, 'Alice', @exp)";
        cmd.Parameters.AddWithValue("@g", groupId);
        cmd.Parameters.AddWithValue("@name", itemName);
        cmd.Parameters.AddWithValue("@at", purchasedAt);
        cmd.Parameters.AddWithValue("@exp", (object?)expDate ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task GetExpiryDaySuggestionsAsync_ReturnsMostFrequentDays_WhenSimilarItemsExist()
    {
        // 3× milk bought, shelf life 7 days each
        InsertPurchase(1, "milk 1L", "2026-05-01", "2026-05-08");
        InsertPurchase(1, "milk 2L", "2026-05-02", "2026-05-09");
        InsertPurchase(1, "milk fat-free", "2026-05-03", "2026-05-10");
        // 1× milk with 14-day shelf life
        InsertPurchase(1, "milk organic", "2026-05-01", "2026-05-15");

        var suggestions = await this.repository.GetExpiryDaySuggestionsAsync(1, "milk");

        Assert.Contains(7, suggestions);
        Assert.Equal(7, suggestions[0]); // 7 days is most frequent
        Assert.Contains(14, suggestions);
    }

    [Fact]
    public async Task GetExpiryDaySuggestionsAsync_ReturnsEmpty_WhenNoMatchingItemNames()
    {
        InsertPurchase(1, "bread", "2026-05-01", "2026-05-04");

        var suggestions = await this.repository.GetExpiryDaySuggestionsAsync(1, "milk");

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task GetExpiryDaySuggestionsAsync_ExcludesRowsWithNullExpDate()
    {
        InsertPurchase(1, "milk", "2026-05-01", null);

        var suggestions = await this.repository.GetExpiryDaySuggestionsAsync(1, "milk");

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task GetExpiryDaySuggestionsAsync_ReturnsEmpty_WhenGroupHasNoData()
    {
        var suggestions = await this.repository.GetExpiryDaySuggestionsAsync(1, "milk");

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task GetAverageExpiryDaysAsync_ReturnsRoundedAverage_WhenDataExists()
    {
        InsertPurchase(1, "bread", "2026-05-01", "2026-05-04");   // 3 days
        InsertPurchase(1, "yogurt", "2026-05-01", "2026-05-08");  // 7 days

        var avg = await this.repository.GetAverageExpiryDaysAsync(1);

        Assert.Equal(5, avg); // (3+7)/2 = 5
    }

    [Fact]
    public async Task GetAverageExpiryDaysAsync_ReturnsZero_WhenNoRowsWithExpDate()
    {
        InsertPurchase(1, "milk", "2026-05-01", null);

        var avg = await this.repository.GetAverageExpiryDaysAsync(1);

        Assert.Equal(0, avg);
    }

    [Fact]
    public async Task GetAverageExpiryDaysAsync_ReturnsZero_WhenGroupHasNoHistory()
    {
        var avg = await this.repository.GetAverageExpiryDaysAsync(1);

        Assert.Equal(0, avg);
    }
}
