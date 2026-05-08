using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class HistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly HistoryRepository repository;

    public HistoryRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=file:historytest?mode=memory&cache=shared");
        this.connection.Open();

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS BotActionHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                UserName TEXT NOT NULL,
                ActionType TEXT NOT NULL,
                Payload TEXT NOT NULL,
                RecordedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );";
        cmd.ExecuteNonQuery();

        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM BotActionHistory;
            DELETE FROM sqlite_sequence WHERE name = 'BotActionHistory';";
        cleanCmd.ExecuteNonQuery();

        this.repository = new HistoryRepository("Data Source=file:historytest?mode=memory&cache=shared");
    }

    [Fact]
    public async Task RecordAsync_Inserts_Row_With_Correct_Fields()
    {
        await this.repository.RecordAsync(
            chatId: 100L,
            userId: 200L,
            userName: "Alice",
            actionType: BotActionType.ItemAdded,
            payloadJson: "{\"name\":\"Milk\"}",
            ct: CancellationToken.None);

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = "SELECT ChatId, UserId, UserName, ActionType, Payload FROM BotActionHistory WHERE ChatId = 100";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(100L, reader.GetInt64(0));
        Assert.Equal(200L, reader.GetInt64(1));
        Assert.Equal("Alice", reader.GetString(2));
        Assert.Equal("ItemAdded", reader.GetString(3));
        Assert.Equal("{\"name\":\"Milk\"}", reader.GetString(4));
        Assert.False(reader.Read());
    }

    [Fact]
    public async Task RecordAsync_Stores_ActionType_As_String()
    {
        await this.repository.RecordAsync(100L, 200L, "Bob", BotActionType.ItemBought, "{}", CancellationToken.None);

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = "SELECT ActionType FROM BotActionHistory";
        var result = cmd.ExecuteScalar() as string;

        Assert.Equal("ItemBought", result);
    }

    [Fact]
    public async Task GetRecentAsync_Returns_Empty_For_Unknown_Chat()
    {
        var entries = await this.repository.GetRecentAsync(chatId: 999L, limit: 10, ct: CancellationToken.None);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetRecentAsync_Returns_Entries_In_Descending_Order()
    {
        await this.repository.RecordAsync(100L, 1L, "A", BotActionType.ItemAdded, "{}", CancellationToken.None);
        await this.repository.RecordAsync(100L, 2L, "B", BotActionType.ItemBought, "{}", CancellationToken.None);
        await this.repository.RecordAsync(100L, 3L, "C", BotActionType.ItemRemoved, "{}", CancellationToken.None);

        var entries = await this.repository.GetRecentAsync(chatId: 100L, limit: 10, ct: CancellationToken.None);

        Assert.Equal(3, entries.Count);
        Assert.Equal("C", entries[0].UserName);
        Assert.Equal("B", entries[1].UserName);
        Assert.Equal("A", entries[2].UserName);
    }

    [Fact]
    public async Task GetRecentAsync_Enforces_Limit()
    {
        for (var i = 0; i < 5; i++)
        {
            await this.repository.RecordAsync(100L, i, $"User{i}", BotActionType.ListViewed, "{}", CancellationToken.None);
        }

        var entries = await this.repository.GetRecentAsync(chatId: 100L, limit: 3, ct: CancellationToken.None);

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public async Task GetRecentAsync_Scoped_To_ChatId()
    {
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ItemAdded, "{}", CancellationToken.None);
        await this.repository.RecordAsync(200L, 2L, "Bob", BotActionType.ItemAdded, "{}", CancellationToken.None);

        var entries = await this.repository.GetRecentAsync(chatId: 100L, limit: 10, ct: CancellationToken.None);

        Assert.Single(entries);
        Assert.Equal("Alice", entries[0].UserName);
        Assert.Equal(100L, entries[0].ChatId);
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
