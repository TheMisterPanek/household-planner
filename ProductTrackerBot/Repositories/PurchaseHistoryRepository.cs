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
            INSERT INTO PurchaseHistory (GroupId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName)
            VALUES (@groupId, @itemName, @quantity, @storeName, @price, @purchasedAt, @boughtByName);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@groupId", record.GroupId);
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
            SELECT Id, GroupId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName
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
                ItemName = reader.GetString(2),
                Quantity = reader.IsDBNull(3) ? null : reader.GetString(3),
                StoreName = reader.IsDBNull(4) ? null : reader.GetString(4),
                Price = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                PurchasedAt = DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal),
                BoughtByName = reader.GetString(7),
            });
        }

        return records.AsReadOnly();
    }
}
