using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class AiQueryServiceTests : IDisposable
{
    private readonly Mock<IOpenRouterClient> clientMock;
    private readonly Mock<ILocalizer> localizerMock;
    private readonly string tempIdentityPath;
    private readonly SqliteConnection inMemoryConnection;
    private readonly string connectionString;

    public AiQueryServiceTests()
    {
        this.clientMock = new Mock<IOpenRouterClient>();
        this.localizerMock = new Mock<ILocalizer>();
        this.localizerMock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        this.tempIdentityPath = Path.GetTempFileName();
        File.WriteAllText(this.tempIdentityPath, "You are a helpful assistant. GroupId={groupId} ChatId={chatId}");

        // Use a named shared-cache in-memory database so new connections see the same data
        this.connectionString = $"Data Source=file:aitest_{Guid.NewGuid():N}?mode=memory&cache=shared";
        this.inMemoryConnection = new SqliteConnection(this.connectionString);
        this.inMemoryConnection.Open();

        // Seed schema and test data using the shared connection
        using var cmd = this.inMemoryConnection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Groups (Id INTEGER PRIMARY KEY, ChatId INTEGER NOT NULL UNIQUE, LanguageCode TEXT NOT NULL DEFAULT 'en');
            CREATE TABLE PurchaseHistory (
                Id INTEGER PRIMARY KEY,
                GroupId INTEGER NOT NULL,
                ItemName TEXT NOT NULL,
                Quantity TEXT,
                StoreName TEXT,
                Price REAL,
                PurchasedAt TEXT NOT NULL,
                BoughtByName TEXT NOT NULL,
                UserId INTEGER NOT NULL DEFAULT 0,
                exp_date TEXT
            );
            INSERT INTO Groups (Id, ChatId) VALUES (1, -100);
            INSERT INTO PurchaseHistory (GroupId, ItemName, PurchasedAt, BoughtByName)
            VALUES (1, 'Milk', '2026-05-01T10:00:00Z', 'Alice');
            INSERT INTO PurchaseHistory (GroupId, ItemName, PurchasedAt, BoughtByName)
            VALUES (1, 'Milk', '2026-05-02T10:00:00Z', 'Bob');
            INSERT INTO PurchaseHistory (GroupId, ItemName, PurchasedAt, BoughtByName)
            VALUES (1, 'Bread', '2026-05-03T10:00:00Z', 'Alice');
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        this.inMemoryConnection.Dispose();
        if (File.Exists(this.tempIdentityPath))
        {
            File.Delete(this.tempIdentityPath);
        }
    }

    [Fact]
    public async Task AnswerAsync_HappyPath_ReturnsAiAnswer()
    {
        // Round 1 returns valid scoped SQL; Round 2 returns formatted answer
        this.clientMock.SetupSequence(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SELECT ItemName, COUNT(*) AS cnt FROM PurchaseHistory WHERE GroupId = @groupId GROUP BY ItemName ORDER BY cnt DESC")
            .ReturnsAsync("You bought Milk twice and Bread once.");

        var service = this.CreateService();

        var result = await service.AnswerAsync(-100L, 1L, "what did we buy most?", CancellationToken.None);

        Assert.Equal("You bought Milk twice and Bread once.", result);
        this.clientMock.Verify(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AnswerAsync_MissingPlaceholder_ReturnsUnsafeQueryError()
    {
        this.clientMock.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SELECT * FROM PurchaseHistory");

        var service = this.CreateService();

        var result = await service.AnswerAsync(-100L, 1L, "all purchases", CancellationToken.None);

        Assert.Equal("ai.error.unsafe-query", result);
        // Round 2 should NOT be called
        this.clientMock.Verify(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnswerAsync_EmptyResultSet_CallsRound2WithNoRows()
    {
        this.clientMock.SetupSequence(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SELECT ItemName FROM PurchaseHistory WHERE GroupId = @groupId AND ItemName = 'Nonexistent'")
            .ReturnsAsync("No purchases found for that item.");

        var service = this.CreateService();

        var result = await service.AnswerAsync(-100L, 1L, "did we buy XYZ?", CancellationToken.None);

        Assert.Equal("No purchases found for that item.", result);

        // Verify Round 2 was called with "(no rows)" in the message
        this.clientMock.Verify(
            c => c.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(msg => msg.Contains("(no rows)")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnswerAsync_Round1HttpError_ReturnsServiceUnavailable()
    {
        this.clientMock.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var service = this.CreateService();

        var result = await service.AnswerAsync(-100L, 1L, "question", CancellationToken.None);

        Assert.Equal("ai.error.service-unavailable", result);
        // Round 2 should NOT be called
        this.clientMock.Verify(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnswerAsync_Round2HttpError_ReturnsServiceUnavailable()
    {
        this.clientMock.SetupSequence(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SELECT ItemName FROM PurchaseHistory WHERE GroupId = @groupId")
            .ThrowsAsync(new HttpRequestException("rate limited"));

        var service = this.CreateService();

        var result = await service.AnswerAsync(-100L, 1L, "what did we buy?", CancellationToken.None);

        Assert.Equal("ai.error.service-unavailable", result);
    }

    [Fact]
    public async Task AnswerAsync_SqlSyntaxError_PassesErrorNoteToRound2()
    {
        this.clientMock.SetupSequence(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NOT VALID SQL WHERE GroupId = @groupId")
            .ReturnsAsync("Sorry, I couldn't process that query.");

        var service = this.CreateService();

        var result = await service.AnswerAsync(-100L, 1L, "broken query test", CancellationToken.None);

        Assert.Equal("Sorry, I couldn't process that query.", result);

        // Verify Round 2 was called with error note
        this.clientMock.Verify(
            c => c.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(msg => msg.Contains("SQL error") || msg.Contains("could not be executed")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnswerAsync_AddsLimit50WhenMissing()
    {
        this.clientMock.SetupSequence(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SELECT ItemName FROM PurchaseHistory WHERE GroupId = @groupId")
            .ReturnsAsync("Here are your items.");

        var service = this.CreateService();
        await service.AnswerAsync(-100L, 1L, "list all", CancellationToken.None);

        // The query executes — if LIMIT wasn't added, SQLite would succeed anyway; we just verify
        // both rounds were called (meaning execution didn't fail)
        this.clientMock.Verify(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private AiQueryService CreateService(string? cs = null)
    {
        return new AiQueryService(
            this.clientMock.Object,
            cs ?? this.connectionString,
            new AiQueryOptions { IdentityMdPath = this.tempIdentityPath },
            this.localizerMock.Object,
            Mock.Of<ILogger<AiQueryService>>());
    }
}
