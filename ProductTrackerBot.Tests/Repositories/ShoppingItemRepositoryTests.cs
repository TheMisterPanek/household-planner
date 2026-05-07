using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class ShoppingItemRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly ShoppingItemRepository repository;
    private readonly int groupId;

    public ShoppingItemRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=file::memory:?cache=shared");
        this.connection.Open();

        // Initialize schema
        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER
            );
            CREATE TABLE IF NOT EXISTS ShoppingItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                Name TEXT NOT NULL,
                Quantity TEXT,
                AddedByName TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        // Clean any leftover data and reset autoincrement
        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM ShoppingItems;
            DELETE FROM Groups;
            DELETE FROM sqlite_sequence WHERE name IN ('Groups', 'ShoppingItems');";
        cleanCmd.ExecuteNonQuery();

        // Insert test group
        using var insertCmd = this.connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (12345); SELECT last_insert_rowid();";
        this.groupId = (int)(long)insertCmd.ExecuteScalar()!;

        this.repository = new ShoppingItemRepository("Data Source=file::memory:?cache=shared");
    }

    [Fact]
    public async Task Add_Item_Should_Return_Item_With_Id()
    {
        var item = await this.repository.AddAsync(
            groupId: this.groupId,
            name: "Молоко",
            quantity: "2л",
            addedByName: "Иван");

        Assert.Equal(1, item.Id);
        Assert.Equal("Молоко", item.Name);
        Assert.Equal("2л", item.Quantity);
        Assert.Equal("Иван", item.AddedByName);
    }

    [Fact]
    public async Task Add_Item_Without_Quantity_Should_Return_Null_Quantity()
    {
        var item = await this.repository.AddAsync(
            groupId: this.groupId,
            name: "Хлеб",
            quantity: null,
            addedByName: "Мария");

        Assert.Null(item.Quantity);
    }

    [Fact]
    public async Task Get_All_Should_Return_All_Items()
    {
        await this.repository.AddAsync(this.groupId, "Молоко", "2л", "Иван");
        await this.repository.AddAsync(this.groupId, "Хлеб", null, "Мария");
        await this.repository.AddAsync(this.groupId, "Яйца", "10 шт", "Петр");

        var items = await this.repository.GetAllAsync(this.groupId);

        Assert.Equal(3, items.Count);
        Assert.Contains(items, i => i.Name == "Молоко");
        Assert.Contains(items, i => i.Name == "Хлеб");
        Assert.Contains(items, i => i.Name == "Яйца");
    }

    [Fact]
    public async Task Get_All_Empty_List_Should_Return_Empty()
    {
        var items = await this.repository.GetAllAsync(this.groupId);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Delete_Item_Should_Remove_From_List()
    {
        var item = await this.repository.AddAsync(this.groupId, "Молоко", "2л", "Иван");
        await this.repository.AddAsync(this.groupId, "Хлеб", null, "Мария");

        await this.repository.DeleteAsync(item.Id);

        var items = await this.repository.GetAllAsync(this.groupId);
        Assert.Single(items);
        Assert.Equal("Хлеб", items[0].Name);
    }

    [Fact]
    public async Task Items_Are_Sorted_By_Id()
    {
        await this.repository.AddAsync(this.groupId, "C", null, "Test");
        await this.repository.AddAsync(this.groupId, "A", null, "Test");
        await this.repository.AddAsync(this.groupId, "B", null, "Test");

        var items = await this.repository.GetAllAsync(this.groupId);

        Assert.Equal("C", items[0].Name);
        Assert.Equal("A", items[1].Name);
        Assert.Equal("B", items[2].Name);
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
