using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Database;

namespace ProductTrackerBot.Tests.Database;

public class DatabaseInitializerBackfillTests : IDisposable
{
    private readonly string connectionString;
    private readonly SqliteConnection keepAlive;

    public DatabaseInitializerBackfillTests()
    {
        this.connectionString = $"Data Source=file:backfill_{Guid.NewGuid():N}?mode=memory&cache=shared";
        this.keepAlive = new SqliteConnection(this.connectionString);
        this.keepAlive.Open();
    }

    public void Dispose() => this.keepAlive.Dispose();

    private async Task RunInitializerAsync()
    {
        var initializer = new DatabaseInitializer(this.connectionString, Mock.Of<ILogger<DatabaseInitializer>>());
        await initializer.StartAsync(CancellationToken.None);
    }

    private async Task<int> SeedGroupAsync(long chatId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (@chatId); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<int> SeedItemWithCategoryAsync(int groupId, string name, string category)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ShoppingItems (GroupId, Name, AddedByName, Category)
            VALUES (@groupId, @name, 'TestUser', @category);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@category", category);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<int> CountAsync(string sql)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<string?> GetItemCategoryAsync(int itemId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Category FROM ShoppingItems WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", itemId);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    [Fact]
    public async Task Backfill_MigratesSingleItemCategoryIntoTag()
    {
        // First pass creates the schema without any data yet.
        await this.RunInitializerAsync();

        var groupId = await this.SeedGroupAsync(-100);
        var itemId = await this.SeedItemWithCategoryAsync(groupId, "Порошок", "Бытовая химия");

        // Second pass runs the backfill against the now-legacy-categorized row.
        await this.RunInitializerAsync();

        var itemTagCount = await this.CountAsync(
            $"SELECT COUNT(*) FROM ItemTags it JOIN Tags t ON t.Id = it.TagId WHERE it.ItemId = {itemId} AND t.Name = 'Бытовая химия'");
        Assert.Equal(1, itemTagCount);

        // Category column is untouched.
        Assert.Equal("Бытовая химия", await this.GetItemCategoryAsync(itemId));
    }

    [Fact]
    public async Task Backfill_TwoItemsSameGroupSameCategory_ShareOneTag()
    {
        await this.RunInitializerAsync();

        var groupId = await this.SeedGroupAsync(-100);
        await this.SeedItemWithCategoryAsync(groupId, "Машина1", "Авто");
        await this.SeedItemWithCategoryAsync(groupId, "Машина2", "Авто");

        await this.RunInitializerAsync();

        var tagCount = await this.CountAsync($"SELECT COUNT(*) FROM Tags WHERE GroupId = {groupId} AND Name = 'Авто'");
        Assert.Equal(1, tagCount);

        var linkCount = await this.CountAsync(
            $"SELECT COUNT(*) FROM ItemTags it JOIN Tags t ON t.Id = it.TagId WHERE t.GroupId = {groupId} AND t.Name = 'Авто'");
        Assert.Equal(2, linkCount);
    }

    [Fact]
    public async Task Backfill_RunsExactlyOnce_NoDuplicateLinksOnRestart()
    {
        await this.RunInitializerAsync();

        var groupId = await this.SeedGroupAsync(-100);
        var itemId = await this.SeedItemWithCategoryAsync(groupId, "Порошок", "Химия");

        await this.RunInitializerAsync();
        await this.RunInitializerAsync();
        await this.RunInitializerAsync();

        var linkCount = await this.CountAsync($"SELECT COUNT(*) FROM ItemTags WHERE ItemId = {itemId}");
        Assert.Equal(1, linkCount);
    }

    [Fact]
    public async Task Backfill_NoLegacyCategories_CreatesNoTags()
    {
        await this.RunInitializerAsync();

        var groupId = await this.SeedGroupAsync(-100);
        await using (var connection = new SqliteConnection(this.connectionString))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO ShoppingItems (GroupId, Name, AddedByName) VALUES (@groupId, 'Хлеб', 'TestUser')";
            cmd.Parameters.AddWithValue("@groupId", groupId);
            await cmd.ExecuteNonQueryAsync();
        }

        await this.RunInitializerAsync();

        Assert.Equal(0, await this.CountAsync("SELECT COUNT(*) FROM Tags"));
        Assert.Equal(0, await this.CountAsync("SELECT COUNT(*) FROM ItemTags"));
    }
}
