// <copyright file="MealIngredientRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// Data access for the MealIngredients table.
/// </summary>
public class MealIngredientRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="MealIngredientRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public MealIngredientRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Gets all ingredients for a meal.
    /// </summary>
    /// <param name="mealId">The meal ID.</param>
    /// <returns>A list of ingredients for the meal.</returns>
    public virtual async Task<List<MealIngredient>> GetAllAsync(int mealId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, MealId, Name, Quantity FROM MealIngredients WHERE MealId = @mealId ORDER BY Id";
        cmd.Parameters.AddWithValue("@mealId", mealId);

        var ingredients = new List<MealIngredient>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ingredients.Add(new MealIngredient
            {
                Id = reader.GetInt32(0),
                MealId = reader.GetInt32(1),
                Name = reader.GetString(2),
                Quantity = reader.IsDBNull(3) ? null : reader.GetString(3),
            });
        }

        return ingredients;
    }

    /// <summary>
    /// Adds a new ingredient to a meal.
    /// </summary>
    /// <param name="mealId">The meal ID.</param>
    /// <param name="name">The ingredient name.</param>
    /// <param name="quantity">The optional quantity.</param>
    /// <returns>The created ingredient.</returns>
    public virtual async Task<MealIngredient> AddAsync(int mealId, string name, string? quantity = null)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO MealIngredients (MealId, Name, Quantity) VALUES (@mealId, @name, @quantity); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@mealId", mealId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@quantity", (object?)quantity ?? DBNull.Value);

        var newId = (long)(await cmd.ExecuteScalarAsync())!;

        return new MealIngredient
        {
            Id = (int)newId,
            MealId = mealId,
            Name = name,
            Quantity = quantity,
        };
    }

    /// <summary>
    /// Deletes an ingredient.
    /// </summary>
    /// <param name="ingredientId">The ingredient ID.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task DeleteAsync(int ingredientId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM MealIngredients WHERE Id = @ingredientId";
        cmd.Parameters.AddWithValue("@ingredientId", ingredientId);

        await cmd.ExecuteNonQueryAsync();
    }
}
