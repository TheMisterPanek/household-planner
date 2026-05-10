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

    [Fact]
    public async Task GetTopShopsAsync_Returns_Shops_Ordered_By_Frequency()
    {
        var t1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        var t4 = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc);
        await InsertRecordWithShopAsync(1, "Milk", "Alice", "Carrefour", purchasedAt: t1, userId: 10);
        await InsertRecordWithShopAsync(1, "Bread", "Alice", "Carrefour", purchasedAt: t2, userId: 10);
        await InsertRecordWithShopAsync(1, "Butter", "Alice", "Stokrotka", purchasedAt: t4, userId: 10);
        await InsertRecordWithShopAsync(1, "Cheese", "Alice", "Decathlon", purchasedAt: t3, userId: 10);

        var shops = await this.repository.GetTopShopsAsync(1, 10, 5);

        Assert.Equal(3, shops.Count);
        Assert.Equal("Carrefour", shops[0]);
        Assert.Equal("Stokrotka", shops[1]);
        Assert.Equal("Decathlon", shops[2]);
    }

    [Fact]
    public async Task GetTopShopsAsync_Breaks_Frequency_Ties_By_Most_Recent_PurchasedAt()
    {
        var date1 = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);
        var date3 = new DateTime(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);
        var date4 = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

        await InsertRecordWithShopAsync(1, "Milk", "Alice", "Shop A", date1, userId: 10);
        await InsertRecordWithShopAsync(1, "Bread", "Alice", "Shop A", date2, userId: 10);
        await InsertRecordWithShopAsync(1, "Butter", "Alice", "Shop B", date3, userId: 10);
        await InsertRecordWithShopAsync(1, "Cheese", "Alice", "Shop B", date4, userId: 10);

        var shops = await this.repository.GetTopShopsAsync(1, 10, 5);

        Assert.Equal(2, shops.Count);
        Assert.Equal("Shop B", shops[0]);
        Assert.Equal("Shop A", shops[1]);
    }

    [Fact]
    public async Task GetTopShopsAsync_Excludes_Null_StoreName_Values()
    {
        await InsertRecordAsync(1, "Milk", "Alice", userId: 10);
        await InsertRecordWithShopAsync(1, "Bread", "Alice", "Carrefour", userId: 10);

        var shops = await this.repository.GetTopShopsAsync(1, 10, 5);

        Assert.Single(shops);
        Assert.Equal("Carrefour", shops[0]);
    }

    [Fact]
    public async Task GetTopShopsAsync_Respects_Limit_Parameter()
    {
        await InsertRecordWithShopAsync(1, "Milk", "Alice", "Shop A", userId: 10);
        await InsertRecordWithShopAsync(1, "Bread", "Alice", "Shop B", userId: 10);
        await InsertRecordWithShopAsync(1, "Butter", "Alice", "Shop C", userId: 10);
        await InsertRecordWithShopAsync(1, "Cheese", "Alice", "Shop D", userId: 10);
        await InsertRecordWithShopAsync(1, "Eggs", "Alice", "Shop E", userId: 10);

        var shops = await this.repository.GetTopShopsAsync(1, 10, 3);

        Assert.Equal(3, shops.Count);
    }

    [Fact]
    public async Task GetTopShopsAsync_Returns_Empty_List_When_User_Has_No_Purchases_In_Group()
    {
        await InsertRecordWithShopAsync(1, "Milk", "Alice", "Carrefour", userId: 10);

        var shops = await this.repository.GetTopShopsAsync(1, 999, 5);

        Assert.Empty(shops);
    }

    [Fact]
    public async Task GetTopShopsAsync_Is_Scoped_To_GroupId_And_UserId()
    {
        await InsertRecordWithShopAsync(1, "Milk", "Alice", "Carrefour", userId: 10);
        await InsertRecordWithShopAsync(2, "Bread", "Bob", "Stokrotka", userId: 20);

        var shops1 = await this.repository.GetTopShopsAsync(1, 10, 5);
        var shops2 = await this.repository.GetTopShopsAsync(2, 20, 5);

        Assert.Single(shops1);
        Assert.Equal("Carrefour", shops1[0]);
        Assert.Single(shops2);
        Assert.Equal("Stokrotka", shops2[0]);
    }

    [Fact]
    public async Task SearchAsync_Is_Case_Insensitive_For_Cyrillic()
    {
        await InsertRecordAsync(1, "Молоко", "Alice");

        var results = await this.repository.SearchAsync(1, "молоко");

        Assert.Single(results);
        Assert.Equal("Молоко", results[0].ItemName);
    }

    [Fact]
    public async Task SearchAsync_Is_Case_Insensitive_For_Polish_Diacritics()
    {
        await InsertRecordAsync(1, "Żółty", "Alice");

        var results = await this.repository.SearchAsync(1, "żółty");

        Assert.Single(results);
        Assert.Equal("Żółty", results[0].ItemName);
    }

    private async Task InsertRecordAsync(int groupId, string itemName, string boughtByName, DateTime? purchasedAt = null, long userId = 123)
    {
        await using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PurchaseHistory (GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName)
            VALUES (@groupId, @userId, @itemName, NULL, NULL, NULL, @purchasedAt, @boughtByName)";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@itemName", itemName);
        cmd.Parameters.AddWithValue("@purchasedAt", (purchasedAt ?? DateTime.UtcNow).ToString("O"));
        cmd.Parameters.AddWithValue("@boughtByName", boughtByName);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertRecordWithShopAsync(int groupId, string itemName, string boughtByName, string storeName, DateTime? purchasedAt = null, long userId = 123)
    {
        await using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PurchaseHistory (GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName)
            VALUES (@groupId, @userId, @itemName, NULL, @storeName, NULL, @purchasedAt, @boughtByName)";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@itemName", itemName);
        cmd.Parameters.AddWithValue("@storeName", storeName);
        cmd.Parameters.AddWithValue("@purchasedAt", (purchasedAt ?? DateTime.UtcNow).ToString("O"));
        cmd.Parameters.AddWithValue("@boughtByName", boughtByName);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
