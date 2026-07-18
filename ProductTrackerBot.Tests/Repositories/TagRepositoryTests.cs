using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class TagRepositoryTests : IDisposable
{
    private const string ConnectionString = "Data Source=file:TagRepoTests?mode=memory&cache=shared";

    private readonly SqliteConnection connection;
    private readonly TagRepository repository;
    private readonly ShoppingItemRepository itemRepository;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly int groupId;

    public TagRepositoryTests()
    {
        this.connection = new SqliteConnection(ConnectionString);
        this.connection.Open();

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER,
                LanguageCode TEXT NOT NULL DEFAULT 'ru'
            );
            CREATE TABLE IF NOT EXISTS ShoppingItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                Name TEXT NOT NULL,
                Quantity TEXT,
                exp_date TEXT,
                AddedByName TEXT NOT NULL
            );
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
            );
            CREATE TABLE IF NOT EXISTS Tags (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                UNIQUE (GroupId, Name COLLATE NOCASE)
            );
            CREATE TABLE IF NOT EXISTS ItemTags (
                ItemId INTEGER NOT NULL,
                TagId INTEGER NOT NULL,
                PRIMARY KEY (ItemId, TagId)
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
            DELETE FROM ItemTags;
            DELETE FROM Tags;
            DELETE FROM PurchaseHistory;
            DELETE FROM ShoppingItems;
            DELETE FROM Groups;
            DELETE FROM sqlite_sequence WHERE name IN ('Groups', 'ShoppingItems', 'Tags', 'PurchaseHistory');";
        cleanCmd.ExecuteNonQuery();

        using var insertCmd = this.connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (12345); SELECT last_insert_rowid();";
        this.groupId = (int)(long)insertCmd.ExecuteScalar()!;

        this.repository = new TagRepository(ConnectionString);
        this.itemRepository = new ShoppingItemRepository(ConnectionString);
        this.purchaseRepository = new PurchaseHistoryRepository(ConnectionString);
    }

    [Fact]
    public async Task GetOrCreateAsync_NewTag_CreatesRow()
    {
        var tagId = await this.repository.GetOrCreateAsync(this.groupId, "Химия");

        Assert.True(tagId > 0);
    }

    [Fact]
    public async Task GetOrCreateAsync_ExistingTag_CaseInsensitive_ReturnsSameId()
    {
        var id1 = await this.repository.GetOrCreateAsync(this.groupId, "Химия");
        var id2 = await this.repository.GetOrCreateAsync(this.groupId, "химия");

        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task SetItemTagsAsync_SingleId_AppliesTagSet()
    {
        var item = await this.itemRepository.AddAsync(this.groupId, "Молоко", "2л", "Иван");

        await this.repository.SetItemTagsAsync(new[] { item.Id }, this.groupId, new[] { "Молочка", "Скидка" });

        var reloaded = await this.itemRepository.GetByIdAsync(item.Id);
        Assert.Equal(2, reloaded!.Tags.Count);
        Assert.Contains("Молочка", reloaded.Tags);
        Assert.Contains("Скидка", reloaded.Tags);
    }

    [Fact]
    public async Task SetItemTagsAsync_MultipleIds_AppliesToAll()
    {
        var item1 = await this.itemRepository.AddAsync(this.groupId, "Молоко", null, "Иван");
        var item2 = await this.itemRepository.AddAsync(this.groupId, "Яйца", null, "Иван");

        await this.repository.SetItemTagsAsync(new[] { item1.Id, item2.Id }, this.groupId, new[] { "Бакалея" });

        var reloaded1 = await this.itemRepository.GetByIdAsync(item1.Id);
        var reloaded2 = await this.itemRepository.GetByIdAsync(item2.Id);
        Assert.Contains("Бакалея", reloaded1!.Tags);
        Assert.Contains("Бакалея", reloaded2!.Tags);
    }

    [Fact]
    public async Task SetItemTagsAsync_ReplacesExistingSet()
    {
        var item = await this.itemRepository.AddAsync(this.groupId, "Молоко", null, "Иван");
        await this.repository.SetItemTagsAsync(new[] { item.Id }, this.groupId, new[] { "Старый" });

        await this.repository.SetItemTagsAsync(new[] { item.Id }, this.groupId, new[] { "Новый" });

        var reloaded = await this.itemRepository.GetByIdAsync(item.Id);
        Assert.DoesNotContain("Старый", reloaded!.Tags);
        Assert.Contains("Новый", reloaded.Tags);
    }

    [Fact]
    public async Task SetItemTagsAsync_EmptySet_ClearsTags()
    {
        var item = await this.itemRepository.AddAsync(this.groupId, "Молоко", null, "Иван");
        await this.repository.SetItemTagsAsync(new[] { item.Id }, this.groupId, new[] { "Старый" });

        await this.repository.SetItemTagsAsync(new[] { item.Id }, this.groupId, Array.Empty<string>());

        var reloaded = await this.itemRepository.GetByIdAsync(item.Id);
        Assert.Empty(reloaded!.Tags);
    }

    [Fact]
    public async Task GetDistinctTagsAsync_Empty_ReturnsEmpty()
    {
        var tags = await this.repository.GetDistinctTagsAsync(this.groupId);

        Assert.Empty(tags);
    }

    [Fact]
    public async Task GetDistinctTagsAsync_ReturnsAlphabeticalDistinctValues()
    {
        var item1 = await this.itemRepository.AddAsync(this.groupId, "Порошок", null, "Иван");
        var item2 = await this.itemRepository.AddAsync(this.groupId, "Машина", null, "Иван");
        var item3 = await this.itemRepository.AddAsync(this.groupId, "Отбеливатель", null, "Иван");
        await this.repository.SetItemTagsAsync(new[] { item1.Id }, this.groupId, new[] { "Химия" });
        await this.repository.SetItemTagsAsync(new[] { item2.Id }, this.groupId, new[] { "Авто" });
        await this.repository.SetItemTagsAsync(new[] { item3.Id }, this.groupId, new[] { "Химия" });

        var tags = await this.repository.GetDistinctTagsAsync(this.groupId);

        Assert.Equal(new[] { "Авто", "Химия" }, tags);
    }

    [Fact]
    public async Task GetDistinctTagsAsync_IsCaseInsensitiveAcrossItems()
    {
        var item1 = await this.itemRepository.AddAsync(this.groupId, "A", null, "Иван");
        var item2 = await this.itemRepository.AddAsync(this.groupId, "B", null, "Иван");
        await this.repository.SetItemTagsAsync(new[] { item1.Id }, this.groupId, new[] { "Химия" });
        await this.repository.SetItemTagsAsync(new[] { item2.Id }, this.groupId, new[] { "химия" });

        var tags = await this.repository.GetDistinctTagsAsync(this.groupId);

        Assert.Single(tags);
    }

    [Fact]
    public async Task GetTopTagsAsync_Empty_ReturnsEmpty()
    {
        var tags = await this.repository.GetTopTagsAsync(this.groupId, 5);

        Assert.Empty(tags);
    }

    private async Task<int> AddPurchaseHistoryAsync(string itemName)
    {
        var record = await this.purchaseRepository.AddAsync(new PurchaseRecord
        {
            GroupId = this.groupId,
            UserId = 1,
            ItemName = itemName,
            BoughtByName = "Alice",
            PurchasedAt = DateTime.UtcNow,
        });
        return record.Id;
    }

    [Fact]
    public async Task GetTopTagsAsync_RanksByFrequency()
    {
        for (int i = 0; i < 5; i++)
        {
            var id = await this.AddPurchaseHistoryAsync($"Item{i}");
            await this.repository.LinkPurchaseHistoryTagsAsync(id, this.groupId, new[] { "Химия" });
        }

        for (int i = 0; i < 2; i++)
        {
            var id = await this.AddPurchaseHistoryAsync($"Car{i}");
            await this.repository.LinkPurchaseHistoryTagsAsync(id, this.groupId, new[] { "Авто" });
        }

        var aspirinId = await this.AddPurchaseHistoryAsync("Aspirin");
        await this.repository.LinkPurchaseHistoryTagsAsync(aspirinId, this.groupId, new[] { "Аптека" });

        var tags = await this.repository.GetTopTagsAsync(this.groupId, 3);

        Assert.Equal(new[] { "Химия", "Авто", "Аптека" }, tags);
    }

    [Fact]
    public async Task GetTopTagsAsync_TruncatesLongLabelsForDisplay()
    {
        var longTag = new string('A', 25);
        var id = await this.AddPurchaseHistoryAsync("Item");
        await this.repository.LinkPurchaseHistoryTagsAsync(id, this.groupId, new[] { longTag });

        var tags = await this.repository.GetTopTagsAsync(this.groupId, 5);

        Assert.Single(tags);
        Assert.Equal(longTag[..20] + "…", tags[0]);
    }

    [Fact]
    public async Task LinkPurchaseHistoryTagsAsync_ResolvesAndLinksEachTag()
    {
        var id = await this.AddPurchaseHistoryAsync("Порошок");

        await this.repository.LinkPurchaseHistoryTagsAsync(id, this.groupId, new[] { "Химия", "Скидка" });

        var results = await this.purchaseRepository.SearchAsync(this.groupId, "Порошок");
        var record = Assert.Single(results);
        Assert.Equal(2, record.Tags.Count);
        Assert.Contains("Химия", record.Tags);
        Assert.Contains("Скидка", record.Tags);
    }

    [Fact]
    public async Task LinkPurchaseHistoryTagsAsync_EmptySet_LinksNothing()
    {
        var id = await this.AddPurchaseHistoryAsync("Хлеб");

        await this.repository.LinkPurchaseHistoryTagsAsync(id, this.groupId, Array.Empty<string>());

        var results = await this.purchaseRepository.SearchAsync(this.groupId, "Хлеб");
        Assert.Empty(Assert.Single(results).Tags);
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
