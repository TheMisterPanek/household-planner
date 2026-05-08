// <copyright file="PriceLogRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// Repository for accessing and writing price log entries.
/// </summary>
public class PriceLogRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceLogRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public PriceLogRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Adds a new price log entry.
    /// </summary>
    /// <param name="groupId">The group this entry belongs to.</param>
    /// <param name="itemName">The name of the item.</param>
    /// <param name="price">The price paid.</param>
    /// <param name="storeName">The store name (optional).</param>
    /// <param name="loggedAt">The date and time the price was logged.</param>
    /// <returns>The created PriceLogEntry with its assigned Id.</returns>
    public async Task<PriceLogEntry> AddAsync(int groupId, string itemName, decimal price, string? storeName, DateTime loggedAt)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PriceLog (GroupId, ItemName, Price, StoreName, LoggedAt)
            VALUES (@groupId, @itemName, @price, @storeName, @loggedAt);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@itemName", itemName);
        cmd.Parameters.AddWithValue("@price", (double)price);
        cmd.Parameters.AddWithValue("@storeName", (object?)storeName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@loggedAt", loggedAt.ToString("o"));

        var id = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        return new PriceLogEntry(
            (int)id,
            groupId,
            itemName,
            price,
            storeName,
            loggedAt);
    }

    /// <summary>
    /// Gets price statistics for an item (case-insensitive search).
    /// </summary>
    /// <param name="groupId">The group to search within.</param>
    /// <param name="itemName">The item name to search for (case-insensitive exact match).</param>
    /// <returns>PriceStats if matching records exist, null otherwise.</returns>
    public async Task<PriceStats?> GetStatsAsync(int groupId, string itemName)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        // First, get overall statistics
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT
                MIN(Price) as MinPrice,
                AVG(Price) as AvgPrice,
                MAX(Price) as MaxPrice,
                COUNT(*) as Count
            FROM PriceLog
            WHERE GroupId = @groupId AND LOWER(ItemName) = LOWER(@itemName);";

        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@itemName", itemName);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var minPrice = reader.GetDouble(0);
        var avgPrice = reader.GetDouble(1);
        var maxPrice = reader.GetDouble(2);
        var count = reader.GetInt32(3);

        if (count == 0)
        {
            return null;
        }

        // Get per-store statistics
        await using var storeCmd = connection.CreateCommand();
        storeCmd.CommandText = @"
            SELECT
                StoreName,
                MIN(Price) as MinPrice,
                AVG(Price) as AvgPrice,
                MAX(Price) as MaxPrice,
                COUNT(*) as Count
            FROM PriceLog
            WHERE GroupId = @groupId AND LOWER(ItemName) = LOWER(@itemName)
            GROUP BY CASE WHEN StoreName IS NULL THEN '__NULL__' ELSE StoreName END
            ORDER BY Count DESC
            LIMIT 10;";

        storeCmd.Parameters.AddWithValue("@groupId", groupId);
        storeCmd.Parameters.AddWithValue("@itemName", itemName);

        var storeStats = new List<StoreStats>();
        await using var storeReader = await storeCmd.ExecuteReaderAsync();
        while (await storeReader.ReadAsync())
        {
            var storeName = storeReader.IsDBNull(0) ? null : storeReader.GetString(0);
            if (storeName == "__NULL__")
            {
                storeName = null;
            }

            var min = (decimal)storeReader.GetDouble(1);
            var avg = (decimal)storeReader.GetDouble(2);
            var max = (decimal)storeReader.GetDouble(3);
            var storeCount = storeReader.GetInt32(4);

            storeStats.Add(new StoreStats(storeName, min, avg, max, storeCount));
        }

        return new PriceStats(
            (decimal)minPrice,
            (decimal)avgPrice,
            (decimal)maxPrice,
            count,
            new ReadOnlyCollection<StoreStats>(storeStats));
    }
}
