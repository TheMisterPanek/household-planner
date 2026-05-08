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
    /// <returns>The created item.</returns>
    public virtual async Task<ShoppingItem> AddAsync(int groupId, string name, string? quantity, string addedByName)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ShoppingItems (GroupId, Name, Quantity, AddedByName)
            VALUES (@groupId, @name, @quantity, @addedByName);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@quantity", (object?)quantity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@addedByName", addedByName);

        var newId = (long)(await cmd.ExecuteScalarAsync())!;

        return new ShoppingItem
        {
            Id = (int)newId,
            GroupId = groupId,
            Name = name,
            Quantity = quantity,
            AddedByName = addedByName,
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
        cmd.CommandText = "SELECT Id, GroupId, Name, Quantity, AddedByName FROM ShoppingItems WHERE GroupId = @groupId ORDER BY Id";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var items = new List<ShoppingItem>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ShoppingItem
            {
                Id = reader.GetInt32(0),
                GroupId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Quantity = reader.IsDBNull(3) ? null : reader.GetString(3),
                AddedByName = reader.GetString(4),
            });
        }

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
        cmd.CommandText = "SELECT Id, GroupId, Name, Quantity, AddedByName FROM ShoppingItems WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", itemId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ShoppingItem
            {
                Id = reader.GetInt32(0),
                GroupId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Quantity = reader.IsDBNull(3) ? null : reader.GetString(3),
                AddedByName = reader.GetString(4),
            };
        }

        return null;
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

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ShoppingItems WHERE Id = @itemId";
        cmd.Parameters.AddWithValue("@itemId", itemId);

        await cmd.ExecuteNonQueryAsync();
    }
}
