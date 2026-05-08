// <copyright file="DatabaseInitializer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Database;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs database schema initialization on startup using <c>CREATE TABLE IF NOT EXISTS</c>.
/// </summary>
public class DatabaseInitializer : IHostedService
{
    private readonly string connectionString;
    private readonly ILogger<DatabaseInitializer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseInitializer"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="logger">The logger.</param>
    public DatabaseInitializer(string connectionString, ILogger<DatabaseInitializer> logger)
    {
        this.connectionString = connectionString;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Initializing SQLite database schema");

        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);

        var createGroups = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER
            );";

        var createItems = @"
            CREATE TABLE IF NOT EXISTS ShoppingItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                Name TEXT NOT NULL,
                Quantity TEXT,
                AddedByName TEXT NOT NULL
            );";

        await using var cmd1 = connection.CreateCommand();
        cmd1.CommandText = createGroups;
        await cmd1.ExecuteNonQueryAsync(cancellationToken);

        await using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = createItems;
        await cmd2.ExecuteNonQueryAsync(cancellationToken);

        var createHistory = @"
            CREATE TABLE IF NOT EXISTS BotActionHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                UserName TEXT NOT NULL,
                ActionType TEXT NOT NULL,
                Payload TEXT NOT NULL,
                RecordedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );";

        await using var cmd3 = connection.CreateCommand();
        cmd3.CommandText = createHistory;
        await cmd3.ExecuteNonQueryAsync(cancellationToken);

        var createPurchaseHistory = @"
            CREATE TABLE IF NOT EXISTS PurchaseHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                ItemName TEXT NOT NULL,
                Quantity TEXT,
                StoreName TEXT,
                Price REAL,
                PurchasedAt TEXT NOT NULL,
                BoughtByName TEXT NOT NULL
            );";

        await using var cmd4 = connection.CreateCommand();
        cmd4.CommandText = createPurchaseHistory;
        await cmd4.ExecuteNonQueryAsync(cancellationToken);

        this.logger.LogInformation("Database schema initialized successfully");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
