using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

/// <summary>
/// Integration-style tests for the ShoppingList page data access layer.
/// Tests verify the repository operations that the ShoppingList.razor page relies on.
/// </summary>
[Collection("DatabaseTests")]
public class ShoppingListPageTests : IDisposable
{
    private const string ConnectionString = "Data Source=file:ShoppingListPageTests?mode=memory&cache=shared";

    private readonly SqliteConnection connection;
    private readonly ShoppingItemRepository itemRepository;
    private readonly GroupRepository groupRepository;
    private readonly int groupId;

    public ShoppingListPageTests()
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
                AddedByName TEXT NOT NULL,
                Category TEXT
            );";
        cmd.ExecuteNonQuery();

        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM ShoppingItems;
            DELETE FROM Groups;
            DELETE FROM sqlite_sequence WHERE name IN ('ShoppingItems', 'Groups');";
        cleanCmd.ExecuteNonQuery();

        using var insertGroup = this.connection.CreateCommand();
        insertGroup.CommandText = "INSERT INTO Groups (ChatId) VALUES (99001); SELECT last_insert_rowid();";
        this.groupId = (int)(long)insertGroup.ExecuteScalar()!;

        this.itemRepository = new ShoppingItemRepository(ConnectionString);
        this.groupRepository = new GroupRepository(ConnectionString);
    }

    // 4.2 — Page renders item rows from GetAllAsync
    [Fact]
    public async Task GetAllAsync_Returns_Items_For_Group()
    {
        await this.itemRepository.AddAsync(this.groupId, "Milk", "2L", "Web", null);
        await this.itemRepository.AddAsync(this.groupId, "Eggs", "12", "Web", null);

        var items = await this.itemRepository.GetAllAsync(this.groupId);

        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Name == "Milk");
        Assert.Contains(items, i => i.Name == "Eggs");
    }

    // 4.3 — Adding an item calls AddAsync with correct parameters
    [Fact]
    public async Task AddAsync_Persists_Item_With_Correct_Fields()
    {
        var expiry = new DateOnly(2026, 6, 15);
        await this.itemRepository.AddAsync(this.groupId, "Milk", "2L", "Web", expiry);

        var items = await this.itemRepository.GetAllAsync(this.groupId);

        Assert.Single(items);
        Assert.Equal("Milk", items[0].Name);
        Assert.Equal("2L", items[0].Quantity);
        Assert.Equal(expiry, items[0].ExpDate);
    }

    // 4.4 / 4.5 — Clicking ✎ and saving calls UpdateAsync with modified values
    [Fact]
    public async Task UpdateAsync_Modifies_Name_And_Quantity()
    {
        var item = await this.itemRepository.AddAsync(this.groupId, "Old Name", "1L", "Web", null);

        await this.itemRepository.UpdateAsync(item.Id, "New Name", "2L");

        var items = await this.itemRepository.GetAllAsync(this.groupId);
        Assert.Single(items);
        Assert.Equal("New Name", items[0].Name);
        Assert.Equal("2L", items[0].Quantity);
    }

    // 4.6 — Clicking ✕ and Yes calls DeleteAsync
    [Fact]
    public async Task DeleteAsync_Removes_Item_From_List()
    {
        var item = await this.itemRepository.AddAsync(this.groupId, "Milk", "2L", "Web", null);
        await this.itemRepository.AddAsync(this.groupId, "Bread", null, "Web", null);

        await this.itemRepository.DeleteAsync(item.Id);

        var items = await this.itemRepository.GetAllAsync(this.groupId);
        Assert.Single(items);
        Assert.Equal("Bread", items[0].Name);
    }

    // 4.7 — Checking checkbox calls DeleteAsync (mark-done = delete, same as bot)
    [Fact]
    public async Task MarkDone_Via_DeleteAsync_Removes_Item_From_List()
    {
        var item = await this.itemRepository.AddAsync(this.groupId, "Milk", "2L", "Web", null);

        // The page calls DeleteAsync for mark-done, same as bot behavior
        await this.itemRepository.DeleteAsync(item.Id);

        var items = await this.itemRepository.GetAllAsync(this.groupId);
        Assert.Empty(items);
    }

    // 4.1 — Unauthenticated redirect is handled by AuthenticatedPageBase (verified in smoke test)
    // Items are group-scoped via GetOrCreateAsync
    [Fact]
    public async Task GetOrCreateAsync_Creates_Group_For_New_ChatId()
    {
        var group = await this.groupRepository.GetOrCreateAsync(99002L);

        Assert.NotEqual(0, group.Id);
        Assert.Equal(99002L, group.ChatId);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Empty_For_Group_With_No_Items()
    {
        var items = await this.itemRepository.GetAllAsync(this.groupId);

        Assert.Empty(items);
    }

    public void Dispose() => this.connection.Dispose();
}
