// <copyright file="TagRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;

/// <summary>
/// Data access for the Tags, ItemTags, and PurchaseHistoryTags tables.
/// </summary>
public class TagRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public TagRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Looks up a group-scoped tag by name (case-insensitive), creating it if it doesn't exist.
    /// </summary>
    /// <param name="groupId">The owning group ID.</param>
    /// <param name="name">The tag name.</param>
    /// <returns>The tag's ID.</returns>
    public virtual async Task<int> GetOrCreateAsync(int groupId, string name)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();
        return await GetOrCreateTagAsync(connection, groupId, name);
    }

    /// <summary>
    /// Replaces the tag set on one or more items in a single bulk operation: resolves (or creates)
    /// each tag name, then replaces each item's <c>ItemTags</c> rows with exactly the resolved set.
    /// </summary>
    /// <param name="itemIds">The item IDs to update.</param>
    /// <param name="groupId">The owning group ID, used to resolve/create the tags.</param>
    /// <param name="tagNames">The tag names to apply; an empty set clears all tags.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task SetItemTagsAsync(IReadOnlyList<int> itemIds, int groupId, IReadOnlyCollection<string> tagNames)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        var tagIds = new List<int>();
        foreach (var name in tagNames)
        {
            tagIds.Add(await GetOrCreateTagAsync(connection, groupId, name));
        }

        foreach (var itemId in itemIds)
        {
            await using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM ItemTags WHERE ItemId = @itemId";
            deleteCmd.Parameters.AddWithValue("@itemId", itemId);
            await deleteCmd.ExecuteNonQueryAsync();

            foreach (var tagId in tagIds)
            {
                await using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = "INSERT OR IGNORE INTO ItemTags (ItemId, TagId) VALUES (@itemId, @tagId)";
                insertCmd.Parameters.AddWithValue("@itemId", itemId);
                insertCmd.Parameters.AddWithValue("@tagId", tagId);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Gets the distinct tag names currently used by active items in a group, alphabetical (case-insensitive).
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>A read-only list of distinct tag names.</returns>
    public virtual async Task<IReadOnlyList<string>> GetDistinctTagsAsync(int groupId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT DISTINCT t.Name
            FROM Tags t
            INNER JOIN ItemTags it ON it.TagId = t.Id
            INNER JOIN ShoppingItems si ON si.Id = it.ItemId
            WHERE t.GroupId = @groupId AND si.GroupId = @groupId
            ORDER BY t.Name COLLATE NOCASE";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var tags = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tags.Add(reader.GetString(0));
        }

        return tags.AsReadOnly();
    }

    /// <summary>
    /// Gets the top-N tags for a group ranked by frequency of use across <c>PurchaseHistoryTags</c>,
    /// ties broken by most recent linked <c>PurchaseHistory.PurchasedAt</c>. Labels over 20 characters
    /// are truncated with "…" for display; the caller must use <see cref="GetOrCreateAsync"/> or
    /// <see cref="SetItemTagsAsync"/> with the original (untruncated) name when persisting.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="limit">The maximum number of tags to return.</param>
    /// <returns>A read-only list of top tag names, ordered by frequency (descending) then recency (descending).</returns>
    public virtual async Task<IReadOnlyList<string>> GetTopTagsAsync(int groupId, int limit)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT t.Name
            FROM Tags t
            INNER JOIN PurchaseHistoryTags pht ON pht.TagId = t.Id
            INNER JOIN PurchaseHistory ph ON ph.Id = pht.PurchaseHistoryId
            WHERE t.GroupId = @groupId
            GROUP BY t.Id
            ORDER BY COUNT(*) DESC, MAX(ph.PurchasedAt) DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@limit", limit);

        var tags = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            tags.Add(name.Length > 20 ? name[..20] + "…" : name);
        }

        return tags.AsReadOnly();
    }

    /// <summary>
    /// Resolves (or creates) each tag name in a group and links it to a purchase history row.
    /// </summary>
    /// <param name="purchaseHistoryId">The purchase history row ID.</param>
    /// <param name="groupId">The owning group ID, used to resolve/create the tags.</param>
    /// <param name="tagNames">The tag names to link.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task LinkPurchaseHistoryTagsAsync(int purchaseHistoryId, int groupId, IReadOnlyCollection<string> tagNames)
    {
        if (tagNames.Count == 0)
        {
            return;
        }

        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        foreach (var name in tagNames)
        {
            var tagId = await GetOrCreateTagAsync(connection, groupId, name);

            await using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT OR IGNORE INTO PurchaseHistoryTags (PurchaseHistoryId, TagId) VALUES (@historyId, @tagId)";
            insertCmd.Parameters.AddWithValue("@historyId", purchaseHistoryId);
            insertCmd.Parameters.AddWithValue("@tagId", tagId);
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Gets the tag names currently attached to a shopping item.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <returns>A read-only list of tag names.</returns>
    public virtual async Task<IReadOnlyList<string>> GetItemTagsAsync(int itemId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT t.Name
            FROM Tags t
            INNER JOIN ItemTags it ON it.TagId = t.Id
            WHERE it.ItemId = @itemId
            ORDER BY t.Name COLLATE NOCASE";
        cmd.Parameters.AddWithValue("@itemId", itemId);

        var tags = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tags.Add(reader.GetString(0));
        }

        return tags.AsReadOnly();
    }

    private static async Task<int> GetOrCreateTagAsync(SqliteConnection connection, int groupId, string name)
    {
        // SQLite's built-in NOCASE collation only folds ASCII letters, so it cannot be relied on for
        // case-insensitive matching of Cyrillic (or other non-ASCII) tag names — case folding is done
        // in .NET (StringComparer.OrdinalIgnoreCase), which handles Cyrillic correctly.
        await using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = "SELECT Id, Name FROM Tags WHERE GroupId = @groupId";
        selectCmd.Parameters.AddWithValue("@groupId", groupId);
        await using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), name, StringComparison.OrdinalIgnoreCase))
                {
                    return reader.GetInt32(0);
                }
            }
        }

        await using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO Tags (GroupId, Name) VALUES (@groupId, @name);
            SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("@groupId", groupId);
        insertCmd.Parameters.AddWithValue("@name", name);
        var newId = (long)(await insertCmd.ExecuteScalarAsync())!;
        return (int)newId;
    }
}
