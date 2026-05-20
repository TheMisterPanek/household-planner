using Microsoft.Data.Sqlite;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Repositories;

/// <summary>
/// Integration-style tests for the Settings page data access layer.
/// Tests verify the repository operations that Settings.razor relies on.
/// </summary>
[Collection("DatabaseTests")]
public class SettingsPageTests : IDisposable
{
    private const string ConnectionString = "Data Source=file:SettingsPageTests?mode=memory&cache=shared";
    private const long ChatId = 77001L;

    private readonly SqliteConnection connection;
    private readonly PreferenceRepository preferenceRepository;

    public SettingsPageTests()
    {
        this.connection = new SqliteConnection(ConnectionString);
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

        using var clean = this.connection.CreateCommand();
        clean.CommandText = "DELETE FROM UserPreferences;";
        clean.ExecuteNonQuery();

        this.preferenceRepository = new PreferenceRepository(ConnectionString);
    }

    // 7.1 — Page loads and displays current preferences
    [Fact]
    public async Task GetLanguageAsync_And_GetExpiryThresholdAsync_Return_Saved_Values()
    {
        await this.preferenceRepository.SaveLanguageAsync(ChatId, "ru", CancellationToken.None);
        await this.preferenceRepository.SetExpiryThresholdAsync(ChatId, 7, CancellationToken.None);

        var lang = await this.preferenceRepository.GetLanguageAsync(ChatId, CancellationToken.None);
        var days = await this.preferenceRepository.GetExpiryThresholdAsync(ChatId, CancellationToken.None);

        Assert.Equal("ru", lang);
        Assert.Equal(7, days);
    }

    // 7.2 — Clicking Save calls SaveLanguageAsync and SetExpiryThresholdAsync with correct values
    [Fact]
    public async Task SaveLanguageAsync_And_SetExpiryThresholdAsync_Persist_Correctly()
    {
        await this.preferenceRepository.SaveLanguageAsync(ChatId, "pl", CancellationToken.None);
        await this.preferenceRepository.SetExpiryThresholdAsync(ChatId, 5, CancellationToken.None);

        var lang = await this.preferenceRepository.GetLanguageAsync(ChatId, CancellationToken.None);
        var days = await this.preferenceRepository.GetExpiryThresholdAsync(ChatId, CancellationToken.None);

        Assert.Equal("pl", lang);
        Assert.Equal(5, days);
    }

    // 7.3 — Success state: preferences persist after save; GetExpiryThresholdAsync returns default 3 for new user
    [Fact]
    public async Task GetExpiryThresholdAsync_Returns_Default_For_New_User()
    {
        var days = await this.preferenceRepository.GetExpiryThresholdAsync(99999L, CancellationToken.None);

        Assert.Equal(3, days);
    }

    [Fact]
    public async Task SetExpiryThresholdAsync_DoesNotOverwriteLanguage()
    {
        await this.preferenceRepository.SaveLanguageAsync(ChatId, "en", CancellationToken.None);
        await this.preferenceRepository.SetExpiryThresholdAsync(ChatId, 10, CancellationToken.None);

        var lang = await this.preferenceRepository.GetLanguageAsync(ChatId, CancellationToken.None);

        Assert.Equal("en", lang);
    }

    [Fact]
    public async Task SaveLanguageAsync_DoesNotOverwriteExpiryThreshold()
    {
        await this.preferenceRepository.SetExpiryThresholdAsync(ChatId, 14, CancellationToken.None);
        await this.preferenceRepository.SaveLanguageAsync(ChatId, "ru", CancellationToken.None);

        var days = await this.preferenceRepository.GetExpiryThresholdAsync(ChatId, CancellationToken.None);

        Assert.Equal(14, days);
    }

    public void Dispose() => this.connection.Dispose();
}
