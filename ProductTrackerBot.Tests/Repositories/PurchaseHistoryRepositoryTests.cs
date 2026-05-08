using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class PurchaseHistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly PurchaseHistoryRepository repository;

    public PurchaseHistoryRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=file:purchasetest?mode=memory&cache=shared");
        this.connection.Open();

        using var createGroup = this.connection.CreateCommand();
        createGroup.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER
            );";
        createGroup.ExecuteNonQuery();

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS PurchaseHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                ItemName TEXT NOT NULL,
                Quantity TEXT,
                StoreName TEXT,
                Price REAL,
                PurchasedAt TEXT NOT NULL,
                BoughtByName TEXT NOT NULL
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

        using var seedGroup2 = this.connection.CreateCommand();
        seedGroup2.CommandText = "INSERT INTO Groups (ChatId) VALUES (-200)";
        seedGroup2.ExecuteNonQuery();

        this.repository = new PurchaseHistoryRepository("Data Source=file:purchasetest?mode=memory&cache=shared");
    }

    [Fact]
    public async Task AddAsync_Saves_All_Fields()
    {
        var record = new PurchaseRecord
        {
            GroupId = 1,
            ItemName = "Milk",
            Quantity = "2л",
            StoreName = "Magnit",
            Price = 89.90m,
            PurchasedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            BoughtByName = "Alice",
        };

        var saved = await this.repository.AddAsync(record);

        Assert.NotEqual(0, saved.Id);
        Assert.Equal(1, saved.GroupId);
        Assert.Equal("Milk", saved.ItemName);
        Assert.Equal("2л", saved.Quantity);
        Assert.Equal("Magnit", saved.StoreName);
        Assert.Equal(89.90m, saved.Price);
        Assert.Equal("Alice", saved.BoughtByName);
    }

    [Fact]
    public async Task AddAsync_Saves_With_Nullable_Fields_Null()
    {
        var record = new PurchaseRecord
        {
            GroupId = 1,
            ItemName = "Bread",
            Quantity = null,
            StoreName = null,
            Price = null,
            PurchasedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            BoughtByName = "Bob",
        };

        var saved = await this.repository.AddAsync(record);

        Assert.NotEqual(0, saved.Id);
        Assert.Null(saved.Quantity);
        Assert.Null(saved.StoreName);
        Assert.Null(saved.Price);
    }

    [Fact]
    public async Task SearchAsync_Returns_Only_Records_For_Given_GroupId()
    {
        await InsertRecordAsync(1, "Milk", "Alice");
        await InsertRecordAsync(2, "Milk", "Bob");

        var results = await this.repository.SearchAsync(1, "Milk");

        Assert.Single(results);
        Assert.Equal("Alice", results[0].BoughtByName);
    }

    [Fact]
    public async Task SearchAsync_Is_Case_Insensitive()
    {
        await InsertRecordAsync(1, "Milk", "Alice");

        var results = await this.repository.SearchAsync(1, "milk");

        Assert.Single(results);
        Assert.Equal("Milk", results[0].ItemName);
    }

    [Fact]
    public async Task SearchAsync_Matches_Partial_Item_Name()
    {
        await InsertRecordAsync(1, "Oat Milk", "Alice");
        await InsertRecordAsync(1, "Soy Milk", "Bob");

        var results = await this.repository.SearchAsync(1, "Milk");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_Returns_Descending_By_PurchasedAt()
    {
        await InsertRecordAsync(1, "Milk", "Alice", new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        await InsertRecordAsync(1, "Milk", "Bob", new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc));

        var results = await this.repository.SearchAsync(1, "Milk");

        Assert.Equal(2, results.Count);
        Assert.Equal("Bob", results[0].BoughtByName);
        Assert.Equal("Alice", results[1].BoughtByName);
    }

    [Fact]
    public async Task SearchAsync_Returns_Empty_For_Unknown_Group()
    {
        await InsertRecordAsync(1, "Milk", "Alice");

        var results = await this.repository.SearchAsync(999, "Milk");

        Assert.Empty(results);
    }

    private async Task InsertRecordAsync(int groupId, string itemName, string boughtByName, DateTime? purchasedAt = null)
    {
        await using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PurchaseHistory (GroupId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName)
            VALUES (@groupId, @itemName, NULL, NULL, NULL, @purchasedAt, @boughtByName)";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@itemName", itemName);
        cmd.Parameters.AddWithValue("@purchasedAt", (purchasedAt ?? DateTime.UtcNow).ToString("O"));
        cmd.Parameters.AddWithValue("@boughtByName", boughtByName);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
