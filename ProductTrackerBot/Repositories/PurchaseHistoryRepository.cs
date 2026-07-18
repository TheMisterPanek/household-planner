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
            INSERT INTO PurchaseHistory (GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName, exp_date)
            VALUES (@groupId, @userId, @itemName, @quantity, @storeName, @price, @purchasedAt, @boughtByName, @expDate);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@groupId", record.GroupId);
        cmd.Parameters.AddWithValue("@userId", record.UserId);
        cmd.Parameters.AddWithValue("@itemName", record.ItemName);
        cmd.Parameters.AddWithValue("@quantity", (object?)record.Quantity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@storeName", (object?)record.StoreName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@price", (object?)record.Price ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@purchasedAt", record.PurchasedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@boughtByName", record.BoughtByName);
        cmd.Parameters.AddWithValue("@expDate", record.ExpDate.HasValue ? record.ExpDate.Value.ToString("yyyy-MM-dd") : (object?)DBNull.Value);

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
            WHERE GroupId = @groupId
            ORDER BY PurchasedAt DESC";
        cmd.Parameters.AddWithValue("@groupId", groupId);

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

        var matched = records
            .Where(r => r.ItemName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await AttachTagsAsync(connection, matched);

        return matched.AsReadOnly();
    }

    /// <summary>
    /// Retrieves a paginated, filtered page of purchase records for a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Records per page.</param>
    /// <param name="nameFilter">Substring filter on ItemName (empty = all).</param>
    /// <param name="dateFrom">Inclusive lower date bound (null = no lower bound).</param>
    /// <param name="dateTo">Inclusive upper date bound (null = no upper bound).</param>
    /// <returns>Matching records for the requested page and total matching count.</returns>
    public virtual async Task<(List<PurchaseRecord> Items, int TotalCount)> GetPageAsync(
        int groupId,
        int page,
        int pageSize,
        string nameFilter,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        var filter = string.IsNullOrEmpty(nameFilter) ? string.Empty : nameFilter;
        var fromStr = dateFrom?.ToString("yyyy-MM-dd");
        var toStr = dateTo?.ToString("yyyy-MM-dd");
        var offset = (page - 1) * pageSize;

        await using var countCmd = connection.CreateCommand();
        countCmd.CommandText = @"
            SELECT COUNT(*) FROM PurchaseHistory
            WHERE GroupId = @groupId
              AND (@nameFilter = '' OR LOWER(ItemName) LIKE '%' || LOWER(@nameFilter) || '%')
              AND (@dateFrom IS NULL OR PurchasedAt >= @dateFrom)
              AND (@dateTo IS NULL OR PurchasedAt <= @dateTo || 'T23:59:59')";
        countCmd.Parameters.AddWithValue("@groupId", groupId);
        countCmd.Parameters.AddWithValue("@nameFilter", filter);
        countCmd.Parameters.AddWithValue("@dateFrom", (object?)fromStr ?? DBNull.Value);
        countCmd.Parameters.AddWithValue("@dateTo", (object?)toStr ?? DBNull.Value);
        var totalCount = (int)(long)(await countCmd.ExecuteScalarAsync())!;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName
            FROM PurchaseHistory
            WHERE GroupId = @groupId
              AND (@nameFilter = '' OR LOWER(ItemName) LIKE '%' || LOWER(@nameFilter) || '%')
              AND (@dateFrom IS NULL OR PurchasedAt >= @dateFrom)
              AND (@dateTo IS NULL OR PurchasedAt <= @dateTo || 'T23:59:59')
            ORDER BY PurchasedAt DESC
            LIMIT @pageSize OFFSET @offset";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@nameFilter", filter);
        cmd.Parameters.AddWithValue("@dateFrom", (object?)fromStr ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dateTo", (object?)toStr ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

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

        await AttachTagsAsync(connection, records);

        return (records, totalCount);
    }

    private static async Task AttachTagsAsync(SqliteConnection connection, List<PurchaseRecord> records)
    {
        for (int i = 0; i < records.Count; i++)
        {
            await using var tagsCmd = connection.CreateCommand();
            tagsCmd.CommandText = @"
                SELECT t.Name
                FROM Tags t
                INNER JOIN PurchaseHistoryTags pht ON pht.TagId = t.Id
                WHERE pht.PurchaseHistoryId = @historyId
                ORDER BY t.Name COLLATE NOCASE";
            tagsCmd.Parameters.AddWithValue("@historyId", records[i].Id);

            var tags = new List<string>();
            await using var reader = await tagsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tags.Add(reader.GetString(0));
            }

            records[i].Tags = tags.AsReadOnly();
        }
    }

    /// <summary>
    /// Returns distinct shelf-life day counts for items whose name contains <paramref name="keyword"/>,
    /// ordered by frequency descending. Up to 5 rows returned.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="keyword">Case-insensitive substring to match against ItemName.</param>
    /// <returns>Distinct rounded day-count values ordered by frequency.</returns>
    public virtual async Task<IReadOnlyList<int>> GetExpiryDaySuggestionsAsync(int groupId, string keyword)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT CAST(ROUND(julianday(exp_date) - julianday(date(PurchasedAt))) AS INT) AS days,
                   COUNT(*) AS freq
            FROM PurchaseHistory
            WHERE GroupId = @groupId
              AND exp_date IS NOT NULL
              AND LOWER(ItemName) LIKE '%' || LOWER(@keyword) || '%'
            GROUP BY days
            ORDER BY freq DESC
            LIMIT 5";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@keyword", keyword);

        var result = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var days = reader.GetInt32(0);
            if (days > 0)
            {
                result.Add(days);
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Returns the group-wide average shelf-life in days (rounded to nearest integer),
    /// or 0 if no records with both <c>exp_date</c> and <c>PurchasedAt</c> exist.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>Average days rounded, or 0.</returns>
    public virtual async Task<int> GetAverageExpiryDaysAsync(int groupId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT CAST(ROUND(AVG(julianday(exp_date) - julianday(date(PurchasedAt)))) AS INT)
            FROM PurchaseHistory
            WHERE GroupId = @groupId AND exp_date IS NOT NULL";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var result = await cmd.ExecuteScalarAsync();
        if (result is null || result == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Deletes a purchase record by ID.
    /// </summary>
    /// <param name="id">The record ID to delete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task DeleteAsync(int id)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM PurchaseHistory WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieves all purchased items with non-null expiry dates for a group, including their IDs.
    /// </summary>
    /// <param name="groupId">The group ID to filter by.</param>
    /// <returns>A read-only list of purchase records with expiry dates, ordered by expiry date ascending.</returns>
    public virtual async Task<IReadOnlyList<PurchaseRecord>> GetInventoryItemsWithExpiryAsync(int groupId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, GroupId, UserId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName, exp_date
            FROM PurchaseHistory
            WHERE GroupId = @groupId AND exp_date IS NOT NULL
            ORDER BY exp_date ASC";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var items = new List<PurchaseRecord>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new PurchaseRecord
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
                ExpDate = DateOnly.ParseExact(reader.GetString(9), "yyyy-MM-dd"),
            });
        }

        return items.AsReadOnly();
    }

    /// <summary>
    /// Retrieves all bought items with non-null expiry dates for a group.
    /// </summary>
    /// <param name="groupId">The group ID to filter by.</param>
    /// <returns>A read-only list of tuples (ItemName, Quantity, ExpDate) for items with expiry dates.</returns>
    public virtual async Task<IReadOnlyList<(string ItemName, string? Quantity, DateOnly ExpDate)>> GetItemsWithExpiryAsync(int groupId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT ItemName, Quantity, exp_date
            FROM PurchaseHistory
            WHERE GroupId = @groupId AND exp_date IS NOT NULL
            ORDER BY exp_date ASC";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var items = new List<(string, string?, DateOnly)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var itemName = reader.GetString(0);
            var quantity = reader.IsDBNull(1) ? null : reader.GetString(1);
            var expDate = DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd");
            items.Add((itemName, quantity, expDate));
        }

        return items.AsReadOnly();
    }
}
