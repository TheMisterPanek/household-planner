// <copyright file="ShoppingItemRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// Data access for the ShoppingItems table.
/// </summary>
public class ShoppingItemRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingItemRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public ShoppingItemRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Adds a new shopping item.
    /// </summary>
    /// <param name="groupId">The owning group ID.</param>
    /// <param name="name">The item name.</param>
    /// <param name="quantity">Optional quantity.</param>
    /// <param name="addedByName">The display name of the user who added the item.</param>
    /// <param name="expDate">Optional expiry date.</param>
    /// <returns>The created item.</returns>
    public virtual async Task<ShoppingItem> AddAsync(int groupId, string name, string? quantity, string addedByName, DateOnly? expDate = null)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ShoppingItems (GroupId, Name, Quantity, AddedByName, exp_date)
            VALUES (@groupId, @name, @quantity, @addedByName, @expDate);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@quantity", (object?)quantity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@addedByName", addedByName);
        cmd.Parameters.AddWithValue("@expDate", expDate.HasValue ? (object)expDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);

        var newId = (long)(await cmd.ExecuteScalarAsync())!;

        return new ShoppingItem
        {
            Id = (int)newId,
            GroupId = groupId,
            Name = name,
            Quantity = quantity,
            ExpDate = expDate,
            AddedByName = addedByName,
            Tags = Array.Empty<string>(),
        };
    }

    /// <summary>
    /// Gets all shopping items for a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>A read-only list of items.</returns>
    public virtual async Task<IReadOnlyList<ShoppingItem>> GetAllAsync(int groupId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, GroupId, Name, Quantity, exp_date, AddedByName FROM ShoppingItems WHERE GroupId = @groupId ORDER BY Id";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var items = new List<ShoppingItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var expDateStr = reader.IsDBNull(4) ? null : reader.GetString(4);
            var expDate = expDateStr != null ? (DateOnly?)DateOnly.ParseExact(expDateStr, "yyyy-MM-dd") : null;

            items.Add(new ShoppingItem
            {
                Id = reader.GetInt32(0),
                GroupId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Quantity = reader.IsDBNull(3) ? null : reader.GetString(3),
                ExpDate = expDate,
                AddedByName = reader.GetString(5),
            });
        }

        await AttachTagsAsync(connection, items);

        return items.AsReadOnly();
    }

    /// <summary>
    /// Gets a shopping item by ID, or null if not found.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <returns>The item, or null.</returns>
    public virtual async Task<ShoppingItem?> GetByIdAsync(int itemId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, GroupId, Name, Quantity, exp_date, AddedByName FROM ShoppingItems WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", itemId);

        ShoppingItem? item = null;
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                var expDateStr = reader.IsDBNull(4) ? null : reader.GetString(4);
                var expDate = expDateStr != null ? (DateOnly?)DateOnly.ParseExact(expDateStr, "yyyy-MM-dd") : null;

                item = new ShoppingItem
                {
                    Id = reader.GetInt32(0),
                    GroupId = reader.GetInt32(1),
                    Name = reader.GetString(2),
                    Quantity = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ExpDate = expDate,
                    AddedByName = reader.GetString(5),
                };
            }
        }

        if (item is null)
        {
            return null;
        }

        var items = new List<ShoppingItem> { item };
        await AttachTagsAsync(connection, items);
        return items[0];
    }

    private static async Task AttachTagsAsync(SqliteConnection connection, List<ShoppingItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            await using var tagsCmd = connection.CreateCommand();
            tagsCmd.CommandText = @"
                SELECT t.Name
                FROM Tags t
                INNER JOIN ItemTags it ON it.TagId = t.Id
                WHERE it.ItemId = @itemId
                ORDER BY t.Name COLLATE NOCASE";
            tagsCmd.Parameters.AddWithValue("@itemId", items[i].Id);

            var tags = new List<string>();
            await using var reader = await tagsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tags.Add(reader.GetString(0));
            }

            items[i] = items[i] with { Tags = tags.AsReadOnly() };
        }
    }

    /// <summary>
    /// Gets all shopping items with expiry dates for a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>A read-only list of items with expiry dates.</returns>
    public virtual async Task<IReadOnlyList<ShoppingItem>> GetItemsWithExpiryAsync(int groupId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, GroupId, Name, Quantity, exp_date, AddedByName FROM ShoppingItems WHERE GroupId = @groupId AND exp_date IS NOT NULL ORDER BY exp_date";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var items = new List<ShoppingItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var expDateStr = reader.GetString(4);
            var expDate = DateOnly.ParseExact(expDateStr, "yyyy-MM-dd");

            items.Add(new ShoppingItem
            {
                Id = reader.GetInt32(0),
                GroupId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Quantity = reader.IsDBNull(3) ? null : reader.GetString(3),
                ExpDate = expDate,
                AddedByName = reader.GetString(5),
            });
        }

        return items.AsReadOnly();
    }

    /// <summary>
    /// Updates the name and quantity of an existing shopping item.
    /// </summary>
    /// <param name="itemId">The item ID to update.</param>
    /// <param name="name">The new item name.</param>
    /// <param name="quantity">The new quantity, or null to clear it.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task UpdateAsync(int itemId, string name, string? quantity)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE ShoppingItems SET Name = @name, Quantity = @quantity WHERE Id = @id";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@quantity", (object?)quantity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", itemId);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Deletes a shopping item by ID.
    /// </summary>
    /// <param name="itemId">The item ID to delete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task DeleteAsync(int itemId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var deleteTagsCmd = connection.CreateCommand();
        deleteTagsCmd.CommandText = "DELETE FROM ItemTags WHERE ItemId = @itemId";
        deleteTagsCmd.Parameters.AddWithValue("@itemId", itemId);
        await deleteTagsCmd.ExecuteNonQueryAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ShoppingItems WHERE Id = @itemId";
        cmd.Parameters.AddWithValue("@itemId", itemId);

        await cmd.ExecuteNonQueryAsync();
    }
}
