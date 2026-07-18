using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class PurchaseHistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly PurchaseHistoryRepository repository;
    private readonly TagRepository tagRepository;

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
                BoughtByName TEXT NOT NULL,
                exp_date TEXT NULL,
                Category TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS Tags (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                UNIQUE (GroupId, Name COLLATE NOCASE)
            );
            CREATE TABLE IF NOT EXISTS PurchaseHistoryTags (
                PurchaseHistoryId INTEGER NOT NULL,
                TagId INTEGER NOT NULL,
                PRIMARY KEY (PurchaseHistoryId, TagId)
            );";
        cmd.ExecuteNonQuery();

        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM PurchaseHistoryTags;
            DELETE FROM Tags;
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
        this.tagRepository = new TagRepository("Data Source=file:purchasetest?mode=memory&cache=shared");
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

    [Fact]
    public async Task AddAsync_Saves_ExpDate_When_Set()
    {
        var expDate = new DateOnly(2026, 6, 15);
        var record = new PurchaseRecord
        {
            GroupId = 1,
            ItemName = "Yogurt",
            Quantity = "400g",
            StoreName = "Carrefour",
            Price = 45.50m,
            PurchasedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            BoughtByName = "Alice",
            ExpDate = expDate,
        };

        var saved = await this.repository.AddAsync(record);

        Assert.NotEqual(0, saved.Id);
        Assert.Equal(expDate, saved.ExpDate);
    }

    [Fact]
    public async Task AddAsync_Saves_ExpDate_As_Null_When_Not_Set()
    {
        var record = new PurchaseRecord
        {
            GroupId = 1,
            ItemName = "Bread",
            Quantity = null,
            StoreName = "Carrefour",
            Price = 25.00m,
            PurchasedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            BoughtByName = "Bob",
            ExpDate = null,
        };

        var saved = await this.repository.AddAsync(record);

        Assert.Null(saved.ExpDate);
    }

    [Fact]
    public async Task GetItemsWithExpiryAsync_Returns_Only_Records_With_NonNull_ExpDate()
    {
        var expDate = new DateOnly(2026, 6, 15);
        await InsertRecordWithExpiryAsync(1, "Yogurt", "400g", expDate);
        await InsertRecordAsync(1, "Bread", "Alice");

        var items = await this.repository.GetItemsWithExpiryAsync(1);

        Assert.Single(items);
        Assert.Equal("Yogurt", items[0].ItemName);
        Assert.Equal("400g", items[0].Quantity);
        Assert.Equal(expDate, items[0].ExpDate);
    }

    [Fact]
    public async Task GetItemsWithExpiryAsync_Returns_Empty_When_No_Items_With_Expiry()
    {
        await InsertRecordAsync(1, "Bread", "Alice");
        await InsertRecordAsync(1, "Milk", "Bob");

        var items = await this.repository.GetItemsWithExpiryAsync(1);

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetItemsWithExpiryAsync_Is_Scoped_To_GroupId()
    {
        var expDate = new DateOnly(2026, 6, 15);
        await InsertRecordWithExpiryAsync(1, "Yogurt", "400g", expDate);
        await InsertRecordWithExpiryAsync(2, "Cheese", "200g", expDate);

        var items1 = await this.repository.GetItemsWithExpiryAsync(1);
        var items2 = await this.repository.GetItemsWithExpiryAsync(2);

        Assert.Single(items1);
        Assert.Equal("Yogurt", items1[0].ItemName);
        Assert.Single(items2);
        Assert.Equal("Cheese", items2[0].ItemName);
    }

    [Fact]
    public async Task GetItemsWithExpiryAsync_Returns_Items_Ordered_By_ExpDate_Ascending()
    {
        var date1 = new DateOnly(2026, 6, 15);
        var date2 = new DateOnly(2026, 6, 20);
        var date3 = new DateOnly(2026, 6, 10);
        await InsertRecordWithExpiryAsync(1, "Yogurt", "400g", date1);
        await InsertRecordWithExpiryAsync(1, "Cheese", "200g", date2);
        await InsertRecordWithExpiryAsync(1, "Milk", "1L", date3);

        var items = await this.repository.GetItemsWithExpiryAsync(1);

        Assert.Equal(3, items.Count);
        Assert.Equal(date3, items[0].ExpDate);
        Assert.Equal(date1, items[1].ExpDate);
        Assert.Equal(date2, items[2].ExpDate);
    }

    [Fact]
    public async Task GetItemsWithExpiryAsync_Handles_Null_Quantity()
    {
        var expDate = new DateOnly(2026, 6, 15);
        await InsertRecordWithExpiryAsync(1, "Yogurt", null, expDate);

        var items = await this.repository.GetItemsWithExpiryAsync(1);

        Assert.Single(items);
        Assert.Null(items[0].Quantity);
    }

    [Fact]
    public async Task GetPageAsync_Returns_Items_Ordered_By_PurchasedAt_Descending()
    {
        var t1 = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc);
        await InsertRecordAsync(1, "Apple", "Alice", t1);
        await InsertRecordAsync(1, "Banana", "Alice", t3);
        await InsertRecordAsync(1, "Cherry", "Alice", t2);

        var (items, total) = await this.repository.GetPageAsync(1, 1, 50, "", null, null);

        Assert.Equal(3, total);
        Assert.Equal(3, items.Count);
        Assert.Equal("Banana", items[0].ItemName);
        Assert.Equal("Cherry", items[1].ItemName);
        Assert.Equal("Apple", items[2].ItemName);
    }

    [Fact]
    public async Task GetPageAsync_Prev_Button_Disabled_On_Page_1()
    {
        for (int i = 0; i < 60; i++)
            await InsertRecordAsync(1, $"Item{i}", "Alice");

        var (items, total) = await this.repository.GetPageAsync(1, 1, 50, "", null, null);

        Assert.Equal(60, total);
        Assert.Equal(50, items.Count);
        // Prev button should be disabled when page == 1; verify we are on page 1 (first call)
        // Next button disabled condition: page * pageSize >= totalCount → 1 * 50 = 50 < 60, so Next is enabled
        Assert.True(1 * 50 < total);
    }

    [Fact]
    public async Task GetPageAsync_Next_Button_Disabled_When_Last_Page()
    {
        for (int i = 0; i < 60; i++)
            await InsertRecordAsync(1, $"Item{i}", "Alice");

        var (items, total) = await this.repository.GetPageAsync(1, 2, 50, "", null, null);

        Assert.Equal(60, total);
        Assert.Equal(10, items.Count);
        // Next button disabled condition: page * pageSize >= totalCount → 2 * 50 = 100 >= 60 → disabled
        Assert.True(2 * 50 >= total);
    }

    [Fact]
    public async Task GetPageAsync_NameFilter_Narrows_Results()
    {
        await InsertRecordAsync(1, "Milk", "Alice");
        await InsertRecordAsync(1, "Oat Milk", "Alice");
        await InsertRecordAsync(1, "Bread", "Alice");

        var (items, total) = await this.repository.GetPageAsync(1, 1, 50, "milk", null, null);

        Assert.Equal(2, total);
        Assert.Equal(2, items.Count);
        Assert.All(items, r => Assert.Contains("milk", r.ItemName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPageAsync_Resets_Page_To_1_When_Filter_Changes()
    {
        for (int i = 0; i < 60; i++)
            await InsertRecordAsync(1, "Milk", "Alice");

        // Page 2 without filter
        var (allPage2, _) = await this.repository.GetPageAsync(1, 2, 50, "", null, null);
        Assert.Equal(10, allPage2.Count);

        // After applying name filter, page 1 should return filtered results
        var (filtered, filteredTotal) = await this.repository.GetPageAsync(1, 1, 50, "Milk", null, null);
        Assert.Equal(60, filteredTotal);
        Assert.Equal(50, filtered.Count);
    }

    [Fact]
    public async Task GetPageAsync_IsScoped_To_GroupId()
    {
        await InsertRecordAsync(1, "Milk", "Alice");
        await InsertRecordAsync(2, "Bread", "Bob");

        var (items1, total1) = await this.repository.GetPageAsync(1, 1, 50, "", null, null);
        var (items2, total2) = await this.repository.GetPageAsync(2, 1, 50, "", null, null);

        Assert.Equal(1, total1);
        Assert.Equal("Milk", items1[0].ItemName);
        Assert.Equal(1, total2);
        Assert.Equal("Bread", items2[0].ItemName);
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

    private async Task InsertRecordWithExpiryAsync(int groupId, string itemName, string? quantity, DateOnly expDate, DateTime? purchasedAt = null, long userId = 123)
    {
        await using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PurchaseHistory (GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName, exp_date)
            VALUES (@groupId, @userId, @itemName, @quantity, NULL, NULL, @purchasedAt, 'TestUser', @expDate)";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@itemName", itemName);
        cmd.Parameters.AddWithValue("@quantity", quantity ?? (object?)DBNull.Value);
        cmd.Parameters.AddWithValue("@purchasedAt", (purchasedAt ?? DateTime.UtcNow).ToString("O"));
        cmd.Parameters.AddWithValue("@expDate", expDate.ToString("yyyy-MM-dd"));
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task AddAsync_New_Record_Has_No_Tags()
    {
        var record = new PurchaseRecord
        {
            GroupId = 1,
            ItemName = "Порошок",
            PurchasedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            BoughtByName = "Alice",
        };

        var saved = await this.repository.AddAsync(record);

        Assert.Empty(saved.Tags);
    }

    [Fact]
    public async Task SearchAsync_Returns_Linked_Tags()
    {
        var record = await this.repository.AddAsync(new PurchaseRecord
        {
            GroupId = 1,
            ItemName = "Порошок",
            PurchasedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            BoughtByName = "Alice",
        });
        await this.tagRepository.LinkPurchaseHistoryTagsAsync(record.Id, 1, new[] { "Бытовая химия", "Скидка" });

        var results = await this.repository.SearchAsync(1, "Порошок");

        var found = Assert.Single(results);
        Assert.Equal(2, found.Tags.Count);
        Assert.Contains("Бытовая химия", found.Tags);
        Assert.Contains("Скидка", found.Tags);
    }

    [Fact]
    public async Task SearchAsync_With_No_Tags_Returns_Empty_Tags()
    {
        await this.repository.AddAsync(new PurchaseRecord
        {
            GroupId = 1,
            ItemName = "Хлеб",
            PurchasedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            BoughtByName = "Bob",
        });

        var results = await this.repository.SearchAsync(1, "Хлеб");

        var found = Assert.Single(results);
        Assert.Empty(found.Tags);
    }

    [Fact]
    public async Task GetPageAsync_Returns_Linked_Tags()
    {
        var record = await this.repository.AddAsync(new PurchaseRecord
        {
            GroupId = 1,
            ItemName = "Молоко",
            PurchasedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            BoughtByName = "Alice",
        });
        await this.tagRepository.LinkPurchaseHistoryTagsAsync(record.Id, 1, new[] { "Молочка" });

        var (items, totalCount) = await this.repository.GetPageAsync(1, 1, 10, string.Empty, null, null);

        Assert.Equal(1, totalCount);
        Assert.Contains("Молочка", items[0].Tags);
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
