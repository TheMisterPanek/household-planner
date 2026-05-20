// <copyright file="MealRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// Data access for the Meals table.
/// </summary>
public class MealRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="MealRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public MealRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Gets all meals for a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>A list of meals for the group.</returns>
    public virtual async Task<List<Meal>> GetAllAsync(int groupId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, GroupId, Name FROM Meals WHERE GroupId = @groupId ORDER BY Id";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var meals = new List<Meal>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            meals.Add(new Meal
            {
                Id = reader.GetInt32(0),
                GroupId = reader.GetInt32(1),
                Name = reader.GetString(2),
            });
        }

        return meals;
    }

    /// <summary>
    /// Gets a meal by ID.
    /// </summary>
    /// <param name="mealId">The meal ID.</param>
    /// <returns>The meal, or null if not found.</returns>
    public virtual async Task<Meal?> GetByIdAsync(int mealId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, GroupId, Name FROM Meals WHERE Id = @mealId";
        cmd.Parameters.AddWithValue("@mealId", mealId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Meal
            {
                Id = reader.GetInt32(0),
                GroupId = reader.GetInt32(1),
                Name = reader.GetString(2),
            };
        }

        return null;
    }

    /// <summary>
    /// Adds a new meal to a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="name">The meal name.</param>
    /// <returns>The created meal.</returns>
    public virtual async Task<Meal> AddAsync(int groupId, string name)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Meals (GroupId, Name) VALUES (@groupId, @name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@name", name);

        var newId = (long)(await cmd.ExecuteScalarAsync())!;

        return new Meal
        {
            Id = (int)newId,
            GroupId = groupId,
            Name = name,
        };
    }

    /// <summary>
    /// Renames a meal.
    /// </summary>
    /// <param name="mealId">The meal ID.</param>
    /// <param name="name">The new name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task UpdateNameAsync(int mealId, string name)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Meals SET Name = @name WHERE Id = @mealId";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@mealId", mealId);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Deletes a meal (cascades to ingredients and steps).
    /// </summary>
    /// <param name="mealId">The meal ID.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task DeleteAsync(int mealId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Meals WHERE Id = @mealId";
        cmd.Parameters.AddWithValue("@mealId", mealId);

        await cmd.ExecuteNonQueryAsync();
    }
}
