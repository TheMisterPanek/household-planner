using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class GroupRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly GroupRepository repository;

    public GroupRepositoryTests()
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
            );";
        cmd.ExecuteNonQuery();

        // Clean any leftover data
        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = "DELETE FROM Groups; DELETE FROM sqlite_sequence WHERE name='Groups';";
        cleanCmd.ExecuteNonQuery();

        this.repository = new GroupRepository("Data Source=file::memory:?cache=shared");
    }

    [Fact]
    public async Task GetOrCreate_First_Call_Creates_Row()
    {
        var group = await this.repository.GetOrCreateAsync(chatId: 12345);

        Assert.True(group.Id > 0);
        Assert.Equal(12345, group.ChatId);
        Assert.Null(group.ListMessageId);
    }

    [Fact]
    public async Task GetOrCreate_Second_Call_Returns_Same_Row()
    {
        var first = await this.repository.GetOrCreateAsync(chatId: 12345);
        var second = await this.repository.GetOrCreateAsync(chatId: 12345);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ChatId, second.ChatId);
    }

    [Fact]
    public async Task GetOrCreate_Different_ChatIds_Create_Different_Rows()
    {
        var group1 = await this.repository.GetOrCreateAsync(chatId: 100);
        var group2 = await this.repository.GetOrCreateAsync(chatId: 200);

        Assert.NotEqual(group1.Id, group2.Id);
        Assert.Equal(100, group1.ChatId);
        Assert.Equal(200, group2.ChatId);
    }

    [Fact]
    public async Task UpdateListMessageId_Should_Persist()
    {
        var group = await this.repository.GetOrCreateAsync(chatId: 12345);
        await this.repository.UpdateListMessageIdAsync(group.Id, 999);

        // GetOrCreate returns same row, but ListMessageId from fresh read
        var updated = await this.repository.GetOrCreateAsync(chatId: 12345);
        Assert.Equal(999, updated.ListMessageId);
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}