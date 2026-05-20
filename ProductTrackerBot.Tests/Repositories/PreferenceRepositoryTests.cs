using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

[Collection("DatabaseTests")]
public class PreferenceRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly PreferenceRepository repository;

    public PreferenceRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=file:preferencetest?mode=memory&cache=shared");
        this.connection.Open();

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS UserPreferences (
                ChatId INTEGER PRIMARY KEY,
                LanguageCode TEXT NOT NULL DEFAULT 'en',
                ExpiryThresholdDays INTEGER NOT NULL DEFAULT 3,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );";
        cmd.ExecuteNonQuery();

        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = "DELETE FROM UserPreferences;";
        cleanCmd.ExecuteNonQuery();

        this.repository = new PreferenceRepository("Data Source=file:preferencetest?mode=memory&cache=shared");
    }

    [Fact]
    public async Task SaveLanguageAsync_Inserts_Language_Preference()
    {
        await this.repository.SaveLanguageAsync(chatId: 100L, languageCode: "en", ct: CancellationToken.None);

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = "SELECT ChatId, LanguageCode FROM UserPreferences WHERE ChatId = 100";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(100L, reader.GetInt64(0));
        Assert.Equal("en", reader.GetString(1));
        Assert.False(reader.Read());
    }

    [Fact]
    public async Task GetLanguageAsync_Returns_Null_For_Unknown_Chat()
    {
        var result = await this.repository.GetLanguageAsync(chatId: 999L, ct: CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLanguageAsync_Returns_Saved_Language()
    {
        await this.repository.SaveLanguageAsync(100L, "ru", CancellationToken.None);

        var result = await this.repository.GetLanguageAsync(chatId: 100L, ct: CancellationToken.None);

        Assert.Equal("ru", result);
    }

    [Fact]
    public async Task SaveLanguageAsync_Updates_Existing_Preference()
    {
        await this.repository.SaveLanguageAsync(100L, "en", CancellationToken.None);
        await this.repository.SaveLanguageAsync(100L, "fr", CancellationToken.None);

        var result = await this.repository.GetLanguageAsync(chatId: 100L, ct: CancellationToken.None);

        Assert.Equal("fr", result);
    }

    [Fact]
    public async Task Multiple_Chats_Have_Independent_Preferences()
    {
        await this.repository.SaveLanguageAsync(100L, "en", CancellationToken.None);
        await this.repository.SaveLanguageAsync(200L, "ru", CancellationToken.None);

        var result1 = await this.repository.GetLanguageAsync(chatId: 100L, ct: CancellationToken.None);
        var result2 = await this.repository.GetLanguageAsync(chatId: 200L, ct: CancellationToken.None);

        Assert.Equal("en", result1);
        Assert.Equal("ru", result2);
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
