using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Services;

public class ShoppingItemRepositoryUpdateTests : IDisposable
{
    private const string ConnectionString = "Data Source=file:shopitemrepo_update_test?mode=memory&cache=shared";
    private readonly SqliteConnection keepAlive;

    public ShoppingItemRepositoryUpdateTests()
    {
        this.keepAlive = new SqliteConnection(ConnectionString);
        this.keepAlive.Open();

        using var cmd = this.keepAlive.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER
            );
            CREATE TABLE IF NOT EXISTS ShoppingItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Quantity TEXT,
                AddedByName TEXT NOT NULL,
                exp_date TEXT,
                Category TEXT
            );
            DELETE FROM ShoppingItems;
            DELETE FROM Groups;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => this.keepAlive.Dispose();

    private int SeedItem(string name, string? quantity)
    {
        using var cmd = this.keepAlive.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO Groups (ChatId) VALUES (1);
            INSERT INTO ShoppingItems (GroupId, Name, Quantity, AddedByName)
            VALUES (1, @name, @qty, 'Alice');
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@qty", (object?)quantity ?? DBNull.Value);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    [Fact]
    public async Task UpdateAsync_KnownItem_UpdatesNameAndQuantity()
    {
        var itemId = SeedItem("хлеб", null);
        var repo = new ShoppingItemRepository(ConnectionString);

        await repo.UpdateAsync(itemId, "хлеб с маслом", "2 шт");

        var items = await repo.GetAllAsync(1);
        var updated = items.First(i => i.Id == itemId);
        Assert.Equal("хлеб с маслом", updated.Name);
        Assert.Equal("2 шт", updated.Quantity);
    }

    [Fact]
    public async Task UpdateAsync_UnknownItemId_NoExceptionThrown()
    {
        var repo = new ShoppingItemRepository(ConnectionString);
        var ex = await Record.ExceptionAsync(() => repo.UpdateAsync(99999, "ghost", null));
        Assert.Null(ex);
    }
}
