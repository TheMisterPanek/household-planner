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

        // Fetch all rows for this group, filter case-insensitively in C#
        // (SQLite LOWER() does not handle Unicode/Cyrillic without ICU)
        await using var fetchCmd = connection.CreateCommand();
        fetchCmd.CommandText = "SELECT Price, StoreName, ItemName FROM PriceLog WHERE GroupId = @groupId";
        fetchCmd.Parameters.AddWithValue("@groupId", groupId);

        var allRows = new List<(decimal Price, string? StoreName, string ItemName)>();
        await using (var fetchReader = await fetchCmd.ExecuteReaderAsync())
        {
            while (await fetchReader.ReadAsync())
            {
                allRows.Add((
                    (decimal)fetchReader.GetDouble(0),
                    fetchReader.IsDBNull(1) ? null : fetchReader.GetString(1),
                    fetchReader.GetString(2)));
            }
        }

        var filtered = allRows
            .Where(r => r.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0)
        {
            return null;
        }

        var minPrice = filtered.Min(r => r.Price);
        var avgPrice = filtered.Average(r => (double)r.Price);
        var maxPrice = filtered.Max(r => r.Price);
        var count = filtered.Count;

        var storeStats = filtered
            .GroupBy(r => r.StoreName)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new StoreStats(
                g.Key,
                g.Min(r => r.Price),
                (decimal)g.Average(r => (double)r.Price),
                g.Max(r => r.Price),
                g.Count()))
            .ToList();

        return new PriceStats(
            minPrice,
            (decimal)avgPrice,
            maxPrice,
            count,
            new ReadOnlyCollection<StoreStats>(storeStats));
    }
}
