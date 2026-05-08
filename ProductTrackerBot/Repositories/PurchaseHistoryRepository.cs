// <copyright file="PurchaseHistoryRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// Data access for the PurchaseHistory table.
/// </summary>
public class PurchaseHistoryRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="PurchaseHistoryRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public PurchaseHistoryRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Inserts a purchase record and returns it with the new Id.
    /// </summary>
    /// <param name="record">The purchase record to insert.</param>
    /// <returns>The record with its new Id.</returns>
    public virtual async Task<PurchaseRecord> AddAsync(PurchaseRecord record)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO PurchaseHistory (GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName)
            VALUES (@groupId, @userId, @itemName, @quantity, @storeName, @price, @purchasedAt, @boughtByName);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@groupId", record.GroupId);
        cmd.Parameters.AddWithValue("@userId", record.UserId);
        cmd.Parameters.AddWithValue("@itemName", record.ItemName);
        cmd.Parameters.AddWithValue("@quantity", (object?)record.Quantity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@storeName", (object?)record.StoreName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@price", (object?)record.Price ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@purchasedAt", record.PurchasedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@boughtByName", record.BoughtByName);

        var newId = (long)(await cmd.ExecuteScalarAsync())!;
        record.Id = (int)newId;
        return record;
    }

    /// <summary>
    /// Retrieves the top shops for a user in a group, ranked by purchase frequency and recency.
    /// </summary>
    /// <param name="groupId">The group ID to filter by.</param>
    /// <param name="userId">The user ID to filter by.</param>
    /// <param name="limit">The maximum number of shops to return.</param>
    /// <returns>A read-only list of top shop names, ordered by frequency (descending) and most recent purchase date (descending).</returns>
    public virtual async Task<IReadOnlyList<string>> GetTopShopsAsync(int groupId, long userId, int limit)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT StoreName
            FROM PurchaseHistory
            WHERE GroupId = @groupId AND UserId = @userId AND StoreName IS NOT NULL
            GROUP BY StoreName
            ORDER BY COUNT(*) DESC, MAX(PurchasedAt) DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@limit", limit);

        var shops = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var shopName = reader.GetString(0);
            if (shopName.Length > 30)
            {
                shopName = shopName.Substring(0, 30) + "…";
            }

            shops.Add(shopName);
        }

        return shops.AsReadOnly();
    }

    /// <summary>
    /// Searches purchase history by partial item name match (case-insensitive).
    /// </summary>
    /// <param name="groupId">The group ID to search within.</param>
    /// <param name="query">The search query to match against item names.</param>
    /// <returns>A read-only list of matching purchase records ordered by PurchasedAt descending.</returns>
    public virtual async Task<IReadOnlyList<PurchaseRecord>> SearchAsync(int groupId, string query)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName
            FROM PurchaseHistory
            WHERE GroupId = @groupId AND lower(ItemName) LIKE lower(@query)
            ORDER BY PurchasedAt DESC";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@query", $"%{query}%");

        var records = new List<PurchaseRecord>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new PurchaseRecord
            {
                Id = reader.GetInt32(0),
                GroupId = reader.GetInt32(1),
                UserId = reader.GetInt64(2),
                ItemName = reader.GetString(3),
                Quantity = reader.IsDBNull(4) ? null : reader.GetString(4),
                StoreName = reader.IsDBNull(5) ? null : reader.GetString(5),
                Price = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                PurchasedAt = DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal),
                BoughtByName = reader.GetString(8),
            });
        }

        return records.AsReadOnly();
    }
}
