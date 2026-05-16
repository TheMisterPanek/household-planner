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

        var createPriceLog = @"
            CREATE TABLE IF NOT EXISTS PriceLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                ItemName TEXT NOT NULL,
                Price REAL NOT NULL,
                StoreName TEXT,
                LoggedAt TEXT NOT NULL
            );";

        await using var cmd5 = connection.CreateCommand();
        cmd5.CommandText = createPriceLog;
        await cmd5.ExecuteNonQueryAsync(cancellationToken);

        var createMeals = @"
            CREATE TABLE IF NOT EXISTS Meals (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                Name TEXT NOT NULL
            );";

        await using var cmd6 = connection.CreateCommand();
        cmd6.CommandText = createMeals;
        await cmd6.ExecuteNonQueryAsync(cancellationToken);

        var createMealIngredients = @"
            CREATE TABLE IF NOT EXISTS MealIngredients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealId INTEGER NOT NULL REFERENCES Meals(Id),
                Name TEXT NOT NULL,
                Quantity TEXT
            );";

        await using var cmd7 = connection.CreateCommand();
        cmd7.CommandText = createMealIngredients;
        await cmd7.ExecuteNonQueryAsync(cancellationToken);

        var createMealSteps = @"
            CREATE TABLE IF NOT EXISTS MealSteps (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealId INTEGER NOT NULL REFERENCES Meals(Id),
                StepNumber INTEGER NOT NULL,
                Text TEXT NOT NULL
            );";

        await using var cmd8 = connection.CreateCommand();
        cmd8.CommandText = createMealSteps;
        await cmd8.ExecuteNonQueryAsync(cancellationToken);

        var createDayMeals = @"
            CREATE TABLE IF NOT EXISTS DayMeals (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId   INTEGER NOT NULL REFERENCES Groups(Id),
                DayOfWeek INTEGER NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),
                MealId    INTEGER NOT NULL REFERENCES Meals(Id)
            );";

        await using var cmd9 = connection.CreateCommand();
        cmd9.CommandText = createDayMeals;
        await cmd9.ExecuteNonQueryAsync(cancellationToken);

        // Migrate DayMeals: remove UNIQUE(GroupId, DayOfWeek) constraint if present.
        // SQLite doesn't support ALTER TABLE DROP CONSTRAINT, so we recreate the table.
        try
        {
            await using var migrateCmd = connection.CreateCommand();
            migrateCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS DayMeals_New (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId   INTEGER NOT NULL REFERENCES Groups(Id),
                    DayOfWeek INTEGER NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),
                    MealId    INTEGER NOT NULL REFERENCES Meals(Id)
                );
                INSERT INTO DayMeals_New (Id, GroupId, DayOfWeek, MealId)
                    SELECT Id, GroupId, DayOfWeek, MealId FROM DayMeals;
                DROP TABLE DayMeals;
                ALTER TABLE DayMeals_New RENAME TO DayMeals;";
            await migrateCmd.ExecuteNonQueryAsync(cancellationToken);
            this.logger.LogInformation("Migrated DayMeals table to remove UNIQUE constraint");
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Table may not exist yet or already migrated, ignore
        }

        // Migrate Groups table: add LanguageCode column if it doesn't exist
        try
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Groups ADD COLUMN LanguageCode TEXT NOT NULL DEFAULT 'ru'";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            this.logger.LogInformation("Added LanguageCode column to Groups table");
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Column already exists, ignore
            this.logger.LogInformation("LanguageCode column already exists on Groups table");
        }

        // Migrate PurchaseHistory table: add UserId column if it doesn't exist
        try
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE PurchaseHistory ADD COLUMN UserId INTEGER NOT NULL DEFAULT 0";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            this.logger.LogInformation("Added UserId column to PurchaseHistory table");
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Column already exists, ignore
            this.logger.LogInformation("UserId column already exists on PurchaseHistory table");
        }

        // Migrate ShoppingItems table: add exp_date column if it doesn't exist
        try
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE ShoppingItems ADD COLUMN exp_date TEXT";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            this.logger.LogInformation("Added exp_date column to ShoppingItems table");
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Column already exists or duplicate column name, ignore
            this.logger.LogInformation("exp_date column already exists on ShoppingItems table");
        }

        // Migrate PurchaseHistory table: add exp_date column if it doesn't exist
        try
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE PurchaseHistory ADD COLUMN exp_date TEXT NULL";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            this.logger.LogInformation("Added exp_date column to PurchaseHistory table");
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Column already exists or duplicate column name, ignore
            this.logger.LogInformation("exp_date column already exists on PurchaseHistory table");
        }

        // Migrate BotActionHistory table: add revert_payload column if it doesn't exist
        try
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE BotActionHistory ADD COLUMN revert_payload TEXT";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            this.logger.LogInformation("Added revert_payload column to BotActionHistory table");
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            this.logger.LogInformation("revert_payload column already exists on BotActionHistory table");
        }

        // Migrate BotActionHistory table: add reverted_at column if it doesn't exist
        try
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE BotActionHistory ADD COLUMN reverted_at TEXT";
            await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            this.logger.LogInformation("Added reverted_at column to BotActionHistory table");
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            this.logger.LogInformation("reverted_at column already exists on BotActionHistory table");
        }

        this.logger.LogInformation("Database schema initialized successfully");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
