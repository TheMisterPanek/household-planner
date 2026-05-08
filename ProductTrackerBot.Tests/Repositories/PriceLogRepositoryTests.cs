// <copyright file="PriceLogRepositoryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Tests.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Xunit;

/// <summary>
/// Tests for PriceLogRepository.
/// </summary>
public class PriceLogRepositoryTests
{
    private readonly string connectionString = "Data Source=:memory:";

    private async Task InitializeDatabaseAsync()
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER
            );
            CREATE TABLE IF NOT EXISTS PriceLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                ItemName TEXT NOT NULL,
                Price REAL NOT NULL,
                StoreName TEXT,
                LoggedAt TEXT NOT NULL
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int> CreateGroupAsync()
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (12345); SELECT last_insert_rowid();";
        var id = (long)await cmd.ExecuteScalarAsync()!;
        return (int)id;
    }

    [Fact]
    public async Task AddAsync_SavesAllFields_ReturnsRecordWithNonZeroId()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);
        var now = DateTime.UtcNow;

        // Act
        var result = await repo.AddAsync(groupId, "Milk", 3.50m, "Walmart", now);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal(groupId, result.GroupId);
        Assert.Equal("Milk", result.ItemName);
        Assert.Equal(3.50m, result.Price);
        Assert.Equal("Walmart", result.StoreName);
    }

    [Fact]
    public async Task AddAsync_WithNullStoreName_SavesSuccessfully()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);

        // Act
        var result = await repo.AddAsync(groupId, "Milk", 3.50m, null, DateTime.UtcNow);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Null(result.StoreName);
    }

    [Fact]
    public async Task GetStatsAsync_WithMatchingRecords_ReturnsCorrectStats()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);

        await repo.AddAsync(groupId, "Milk", 2.00m, "Store1", DateTime.UtcNow);
        await repo.AddAsync(groupId, "Milk", 3.00m, "Store1", DateTime.UtcNow);
        await repo.AddAsync(groupId, "Milk", 4.00m, "Store1", DateTime.UtcNow);

        // Act
        var stats = await repo.GetStatsAsync(groupId, "Milk");

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2.00m, stats.Min);
        Assert.Equal(3.00m, stats.Avg);
        Assert.Equal(4.00m, stats.Max);
        Assert.Equal(3, stats.Count);
    }

    [Fact]
    public async Task GetStatsAsync_IsCaseInsensitive_FindsItem()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);

        await repo.AddAsync(groupId, "Milk", 2.00m, "Store1", DateTime.UtcNow);
        await repo.AddAsync(groupId, "Milk", 3.00m, "Store1", DateTime.UtcNow);

        // Act
        var stats = await repo.GetStatsAsync(groupId, "milk");

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2, stats.Count);
    }

    [Fact]
    public async Task GetStatsAsync_WithCyrillicCaseInsensitive_FindsItem()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);

        await repo.AddAsync(groupId, "Молоко", 100m, "Магазин", DateTime.UtcNow);
        await repo.AddAsync(groupId, "Молоко", 120m, "Магазин", DateTime.UtcNow);

        // Act
        var stats = await repo.GetStatsAsync(groupId, "молоко");

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2, stats.Count);
    }

    [Fact]
    public async Task GetStatsAsync_WithNoMatches_ReturnsNull()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);

        // Act
        var stats = await repo.GetStatsAsync(groupId, "NonexistentItem");

        // Assert
        Assert.Null(stats);
    }

    [Fact]
    public async Task GetStatsAsync_ScopedToGroup_ExcludesOtherGroups()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId1 = await this.CreateGroupAsync();
        var groupId2 = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);

        await repo.AddAsync(groupId1, "Milk", 2.00m, "Store1", DateTime.UtcNow);
        await repo.AddAsync(groupId2, "Milk", 5.00m, "Store2", DateTime.UtcNow);

        // Act
        var stats1 = await repo.GetStatsAsync(groupId1, "Milk");

        // Assert
        Assert.NotNull(stats1);
        Assert.Equal(2.00m, stats1.Min);
        Assert.Equal(1, stats1.Count);
    }

    [Fact]
    public async Task GetStatsAsync_StoreBreakdown_IncludesPerStoreStats()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);

        await repo.AddAsync(groupId, "Milk", 2.00m, "Store1", DateTime.UtcNow);
        await repo.AddAsync(groupId, "Milk", 3.00m, "Store1", DateTime.UtcNow);
        await repo.AddAsync(groupId, "Milk", 5.00m, "Store2", DateTime.UtcNow);

        // Act
        var stats = await repo.GetStatsAsync(groupId, "Milk");

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2, stats.StoreBreakdown.Count);
        var store1Stats = stats.StoreBreakdown.FirstOrDefault(s => s.StoreName == "Store1");
        Assert.NotNull(store1Stats);
        Assert.Equal(2.00m, store1Stats.Min);
        Assert.Equal(3.00m, store1Stats.Max);
        Assert.Equal(2, store1Stats.Count);
    }

    [Fact]
    public async Task GetStatsAsync_StoreBreakdown_LimitedToTenStores()
    {
        // Arrange
        await this.InitializeDatabaseAsync();
        var groupId = await this.CreateGroupAsync();
        var repo = new PriceLogRepository(this.connectionString);

        // Add 15 different stores
        for (int i = 0; i < 15; i++)
        {
            await repo.AddAsync(groupId, "Milk", 2.00m, $"Store{i}", DateTime.UtcNow);
        }

        // Act
        var stats = await repo.GetStatsAsync(groupId, "Milk");

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.StoreBreakdown.Count <= 10);
    }
}
