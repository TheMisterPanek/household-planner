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
                RecordedAt TEXT NOT NULL DEFAULT (datetime('now')),
                revert_payload TEXT,
                reverted_at TEXT
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
            revertPayloadJson: null,
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
        await this.repository.RecordAsync(100L, 200L, "Bob", BotActionType.ItemBought, "{}", null, CancellationToken.None);

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = "SELECT ActionType FROM BotActionHistory";
        var result = cmd.ExecuteScalar() as string;

        Assert.Equal("ItemBought", result);
    }

    [Fact]
    public async Task RecordAsync_Stores_RevertPayload_When_Provided()
    {
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ItemBought, "{}", "{\"revert\":true}", CancellationToken.None);

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = "SELECT revert_payload FROM BotActionHistory";
        var result = cmd.ExecuteScalar() as string;

        Assert.Equal("{\"revert\":true}", result);
    }

    [Fact]
    public async Task RecordAsync_Stores_Null_RevertPayload_When_Not_Provided()
    {
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ItemAdded, "{}", null, CancellationToken.None);

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = "SELECT revert_payload FROM BotActionHistory";
        var result = cmd.ExecuteScalar();

        Assert.Equal(DBNull.Value, result);
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
        await this.repository.RecordAsync(100L, 1L, "A", BotActionType.ItemAdded, "{}", null, CancellationToken.None);
        await this.repository.RecordAsync(100L, 2L, "B", BotActionType.ItemBought, "{}", null, CancellationToken.None);
        await this.repository.RecordAsync(100L, 3L, "C", BotActionType.ItemRemoved, "{}", null, CancellationToken.None);

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
            await this.repository.RecordAsync(100L, i, $"User{i}", BotActionType.ListViewed, "{}", null, CancellationToken.None);
        }

        var entries = await this.repository.GetRecentAsync(chatId: 100L, limit: 3, ct: CancellationToken.None);

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public async Task GetRecentAsync_Scoped_To_ChatId()
    {
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ItemAdded, "{}", null, CancellationToken.None);
        await this.repository.RecordAsync(200L, 2L, "Bob", BotActionType.ItemAdded, "{}", null, CancellationToken.None);

        var entries = await this.repository.GetRecentAsync(chatId: 100L, limit: 10, ct: CancellationToken.None);

        Assert.Single(entries);
        Assert.Equal("Alice", entries[0].UserName);
        Assert.Equal(100L, entries[0].ChatId);
    }

    [Fact]
    public async Task GetLatestReversibleAsync_Returns_Most_Recent_Reversible_Entry()
    {
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ItemAdded, "{}", null, CancellationToken.None);
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ItemBought, "{}", "{\"revert\":true}", CancellationToken.None);

        var entry = await this.repository.GetLatestReversibleAsync(100L, 1L, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(BotActionType.ItemBought, entry.ActionType);
        Assert.Equal("{\"revert\":true}", entry.RevertPayload);
    }

    [Fact]
    public async Task GetLatestReversibleAsync_Returns_Null_When_No_Reversible_Entry()
    {
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ListViewed, "{}", null, CancellationToken.None);

        var entry = await this.repository.GetLatestReversibleAsync(100L, 1L, CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task GetLatestReversibleAsync_Excludes_Already_Reverted_Entries()
    {
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ItemBought, "{}", "{\"revert\":true}", CancellationToken.None);

        var entry = await this.repository.GetLatestReversibleAsync(100L, 1L, CancellationToken.None);
        Assert.NotNull(entry);

        await this.repository.MarkRevertedAsync(entry.Id, "2026-01-01T00:00:00Z", CancellationToken.None);

        var afterRevert = await this.repository.GetLatestReversibleAsync(100L, 1L, CancellationToken.None);
        Assert.Null(afterRevert);
    }

    [Fact]
    public async Task MarkRevertedAsync_Sets_RevertedAt()
    {
        await this.repository.RecordAsync(100L, 1L, "Alice", BotActionType.ItemBought, "{}", "{\"revert\":true}", CancellationToken.None);

        using var selectCmd = this.connection.CreateCommand();
        selectCmd.CommandText = "SELECT Id FROM BotActionHistory";
        var id = (long)selectCmd.ExecuteScalar()!;

        await this.repository.MarkRevertedAsync(id, "2026-05-13T10:00:00Z", CancellationToken.None);

        using var checkCmd = this.connection.CreateCommand();
        checkCmd.CommandText = "SELECT reverted_at FROM BotActionHistory WHERE Id = @id";
        checkCmd.Parameters.AddWithValue("@id", id);
        var result = checkCmd.ExecuteScalar() as string;

        Assert.Equal("2026-05-13T10:00:00Z", result);
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
