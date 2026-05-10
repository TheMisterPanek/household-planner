using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Localization;

[Collection("DatabaseTests")]
public class LocalizerTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly GroupRepository repository;
    private readonly Mock<ILogger<Localizer>> loggerMock;

    public LocalizerTests()
    {
        this.connection = new SqliteConnection("Data Source=file:LocalizerTests?mode=memory&cache=shared");
        this.connection.Open();

        // Initialize schema
        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER,
                LanguageCode TEXT NOT NULL DEFAULT 'ru'
            );";
        cmd.ExecuteNonQuery();

        // Clean any leftover data
        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = "DELETE FROM Groups; DELETE FROM sqlite_sequence WHERE name='Groups';";
        cleanCmd.ExecuteNonQuery();

        this.repository = new GroupRepository("Data Source=file:LocalizerTests?mode=memory&cache=shared");
        this.loggerMock = new Mock<ILogger<Localizer>>();
    }

    [Fact]
    public async Task Get_ReturnsCorrectString_For_Known_Locale_And_Key()
    {
        // Arrange
        var localizer = new Localizer(this.repository, this.loggerMock.Object);
        var chatId = 12345L;
        var group = await this.repository.GetOrCreateAsync(chatId);
        await this.repository.SetLanguageAsync(chatId, "en");

        // Act
        var result = localizer.Get(chatId, "buy.what-to-buy");

        // Assert
        Assert.Equal("What do you want to buy?", result);
    }

    [Fact]
    public async Task Get_FallsBackToEnglish_For_Unknown_Locale()
    {
        // Arrange
        var localizer = new Localizer(this.repository, this.loggerMock.Object);
        var chatId = 12345L;
        var group = await this.repository.GetOrCreateAsync(chatId);
        await this.repository.SetLanguageAsync(chatId, "unknown");

        // Act
        var result = localizer.Get(chatId, "buy.what-to-buy");

        // Assert
        Assert.Equal("What do you want to buy?", result);
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Missing translation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Get_ReturnsKeyName_And_Logs_Warning_For_Missing_Key()
    {
        // Arrange
        var localizer = new Localizer(this.repository, this.loggerMock.Object);
        var chatId = 12345L;
        var group = await this.repository.GetOrCreateAsync(chatId);
        await this.repository.SetLanguageAsync(chatId, "en");

        // Act
        var result = localizer.Get(chatId, "nonexistent.key");

        // Assert
        Assert.Equal("nonexistent.key", result);
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Missing translation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetLanguageAsync_Persists_And_IsReadable()
    {
        // Arrange
        var chatId = 12345L;
        await this.repository.GetOrCreateAsync(chatId);

        // Act
        await this.repository.SetLanguageAsync(chatId, "en");

        // Assert - Read it back
        var localizer = new Localizer(this.repository, this.loggerMock.Object);
        var result = localizer.Get(chatId, "buy.what-to-buy");
        Assert.Equal("What do you want to buy?", result);

        // Verify by reading directly from repository
        var group = await this.repository.GetOrCreateAsync(chatId);
        Assert.Equal("en", group.LanguageCode);
    }

    public void Dispose()
    {
        this.connection.Dispose();
    }
}
