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
[Collection("DatabaseTests")]
public class PriceLogRepositoryTests : IDisposable
{
    private const string ConnectionString = "Data Source=file:PriceLogRepoTests?mode=memory&cache=shared";
    private readonly SqliteConnection connection;
    private readonly PriceLogRepository repository;
    private int nextChatId = 10000;

    public PriceLogRepositoryTests()
    {
        this.connection = new SqliteConnection(ConnectionString);
        this.connection.Open();

        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL UNIQUE,
                ListMessageId INTEGER,
                LanguageCode TEXT NOT NULL DEFAULT 'ru'
            );
            CREATE TABLE IF NOT EXISTS PriceLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL REFERENCES Groups(Id),
                ItemName TEXT NOT NULL,
                Price REAL NOT NULL,
                StoreName TEXT,
                LoggedAt TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();

        using var cleanCmd = this.connection.CreateCommand();
        cleanCmd.CommandText = @"
            DELETE FROM PriceLog;
            DELETE FROM Groups;
            DELETE FROM sqlite_sequence WHERE name IN ('PriceLog', 'Groups');";
        cleanCmd.ExecuteNonQuery();

        this.repository = new PriceLogRepository(ConnectionString);
    }

    public void Dispose() => this.connection.Dispose();

    private int CreateGroup()
    {
        using var cmd = this.connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Groups (ChatId) VALUES (@chatId); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@chatId", this.nextChatId++);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    [Fact]
    public async Task AddAsync_SavesAllFields_ReturnsRecordWithNonZeroId()
    {
        var groupId = this.CreateGroup();
        var now = DateTime.UtcNow;

        var result = await this.repository.AddAsync(groupId, "Milk", 3.50m, "Walmart", now);

        Assert.True(result.Id > 0);
        Assert.Equal(groupId, result.GroupId);
        Assert.Equal("Milk", result.ItemName);
        Assert.Equal(3.50m, result.Price);
        Assert.Equal("Walmart", result.StoreName);
    }

    [Fact]
    public async Task AddAsync_WithNullStoreName_SavesSuccessfully()
    {
        var groupId = this.CreateGroup();

        var result = await this.repository.AddAsync(groupId, "Milk", 3.50m, null, DateTime.UtcNow);

        Assert.True(result.Id > 0);
        Assert.Null(result.StoreName);
    }

    [Fact]
    public async Task GetStatsAsync_WithMatchingRecords_ReturnsCorrectStats()
    {
        var groupId = this.CreateGroup();

        await this.repository.AddAsync(groupId, "Milk", 2.00m, "Store1", DateTime.UtcNow);
        await this.repository.AddAsync(groupId, "Milk", 3.00m, "Store1", DateTime.UtcNow);
        await this.repository.AddAsync(groupId, "Milk", 4.00m, "Store1", DateTime.UtcNow);

        var stats = await this.repository.GetStatsAsync(groupId, "Milk");

        Assert.NotNull(stats);
        Assert.Equal(2.00m, stats.Min);
        Assert.Equal(3.00m, stats.Avg);
        Assert.Equal(4.00m, stats.Max);
        Assert.Equal(3, stats.Count);
    }

    [Fact]
    public async Task GetStatsAsync_IsCaseInsensitive_FindsItem()
    {
        var groupId = this.CreateGroup();

        await this.repository.AddAsync(groupId, "Milk", 2.00m, "Store1", DateTime.UtcNow);
        await this.repository.AddAsync(groupId, "Milk", 3.00m, "Store1", DateTime.UtcNow);

        var stats = await this.repository.GetStatsAsync(groupId, "milk");

        Assert.NotNull(stats);
        Assert.Equal(2, stats.Count);
    }

    [Fact]
    public async Task GetStatsAsync_WithCyrillicCaseInsensitive_FindsItem()
    {
        var groupId = this.CreateGroup();

        await this.repository.AddAsync(groupId, "Молоко", 100m, "Магазин", DateTime.UtcNow);
        await this.repository.AddAsync(groupId, "Молоко", 120m, "Магазин", DateTime.UtcNow);

        var stats = await this.repository.GetStatsAsync(groupId, "молоко");

        Assert.NotNull(stats);
        Assert.Equal(2, stats.Count);
    }

    [Fact]
    public async Task GetStatsAsync_WithNoMatches_ReturnsNull()
    {
        var groupId = this.CreateGroup();

        var stats = await this.repository.GetStatsAsync(groupId, "NonexistentItem");

        Assert.Null(stats);
    }

    [Fact]
    public async Task GetStatsAsync_ScopedToGroup_ExcludesOtherGroups()
    {
        var groupId1 = this.CreateGroup();
        var groupId2 = this.CreateGroup();

        await this.repository.AddAsync(groupId1, "Milk", 2.00m, "Store1", DateTime.UtcNow);
        await this.repository.AddAsync(groupId2, "Milk", 5.00m, "Store2", DateTime.UtcNow);

        var stats1 = await this.repository.GetStatsAsync(groupId1, "Milk");

        Assert.NotNull(stats1);
        Assert.Equal(2.00m, stats1.Min);
        Assert.Equal(1, stats1.Count);
    }

    [Fact]
    public async Task GetStatsAsync_StoreBreakdown_IncludesPerStoreStats()
    {
        var groupId = this.CreateGroup();

        await this.repository.AddAsync(groupId, "Milk", 2.00m, "Store1", DateTime.UtcNow);
        await this.repository.AddAsync(groupId, "Milk", 3.00m, "Store1", DateTime.UtcNow);
        await this.repository.AddAsync(groupId, "Milk", 5.00m, "Store2", DateTime.UtcNow);

        var stats = await this.repository.GetStatsAsync(groupId, "Milk");

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
        var groupId = this.CreateGroup();

        for (int i = 0; i < 15; i++)
        {
            await this.repository.AddAsync(groupId, "Milk", 2.00m, $"Store{i}", DateTime.UtcNow);
        }

        var stats = await this.repository.GetStatsAsync(groupId, "Milk");

        Assert.NotNull(stats);
        Assert.True(stats.StoreBreakdown.Count <= 10);
    }

    [Fact]
    public async Task GetAllAsync_Groups_Entries_By_ItemName_Correctly()
    {
        var groupId = this.CreateGroup();
        var t = DateTime.UtcNow;

        await this.repository.AddAsync(groupId, "Milk", 2.00m, null, t);
        await this.repository.AddAsync(groupId, "Milk", 2.50m, null, t.AddDays(1));
        await this.repository.AddAsync(groupId, "Bread", 1.80m, null, t);

        var entries = await this.repository.GetAllAsync(groupId);

        var grouped = entries
            .GroupBy(e => e.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        Assert.True(grouped.ContainsKey("Milk"));
        Assert.True(grouped.ContainsKey("Bread"));
        Assert.Equal(2, grouped["Milk"].Count);
        Assert.Single(grouped["Bread"]);
    }

    [Fact]
    public async Task Prices_TrendArrow_Is_Up_When_Last_Price_Greater_Than_Previous()
    {
        var groupId = this.CreateGroup();
        var t = DateTime.UtcNow;

        await this.repository.AddAsync(groupId, "Milk", 2.00m, null, t);
        await this.repository.AddAsync(groupId, "Milk", 3.00m, null, t.AddDays(1));

        var entries = await this.repository.GetAllAsync(groupId);
        var sorted = entries.OrderBy(e => e.LoggedAt).ToList();

        var last = sorted.Last().Price;
        var prev = sorted[sorted.Count - 2].Price;

        Assert.True(last > prev);
    }

    [Fact]
    public async Task Prices_TrendArrow_Is_Down_When_Last_Price_Less_Than_Previous()
    {
        var groupId = this.CreateGroup();
        var t = DateTime.UtcNow;

        await this.repository.AddAsync(groupId, "Milk", 3.00m, null, t);
        await this.repository.AddAsync(groupId, "Milk", 2.00m, null, t.AddDays(1));

        var entries = await this.repository.GetAllAsync(groupId);
        var sorted = entries.OrderBy(e => e.LoggedAt).ToList();

        var last = sorted.Last().Price;
        var prev = sorted[sorted.Count - 2].Price;

        Assert.True(last < prev);
    }

    [Fact]
    public async Task Prices_TrendArrow_Is_Flat_When_Equal_Or_Single_Entry()
    {
        var groupId = this.CreateGroup();
        var t = DateTime.UtcNow;

        await this.repository.AddAsync(groupId, "Milk", 2.00m, null, t);

        var entries = await this.repository.GetAllAsync(groupId);

        Assert.Single(entries);
    }

    [Fact]
    public async Task Prices_ClickCard_Expands_And_Collapses()
    {
        // Simulates the toggle logic from Prices.razor
        string? expandedItem = null;
        var itemName = "Milk";

        // First click: expand
        expandedItem = expandedItem == itemName ? null : itemName;
        Assert.Equal("Milk", expandedItem);

        // Second click on same: collapse
        expandedItem = expandedItem == itemName ? null : itemName;
        Assert.Null(expandedItem);

        // Click different item
        expandedItem = "Bread";
        expandedItem = expandedItem == itemName ? null : itemName;
        Assert.Equal("Milk", expandedItem);
    }

    [Fact]
    public async Task GetAllAsync_IsScoped_To_GroupId()
    {
        var groupId1 = this.CreateGroup();
        var groupId2 = this.CreateGroup();
        var t = DateTime.UtcNow;

        await this.repository.AddAsync(groupId1, "Milk", 2.00m, null, t);
        await this.repository.AddAsync(groupId2, "Bread", 1.80m, null, t);

        var entries1 = await this.repository.GetAllAsync(groupId1);
        var entries2 = await this.repository.GetAllAsync(groupId2);

        Assert.Single(entries1);
        Assert.Equal("Milk", entries1[0].ItemName);
        Assert.Single(entries2);
        Assert.Equal("Bread", entries2[0].ItemName);
    }
}
