// <copyright file="DayMealsRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// Data access for the DayMeals table.
/// </summary>
public class DayMealsRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="DayMealsRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public DayMealsRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Gets all day-meal entries for a group's weekly plan.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>A list of day-meal entries ordered by day of week.</returns>
    public virtual async Task<List<DayMealEntry>> GetWeekAsync(int groupId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT dm.DayOfWeek, dm.MealId, m.Name
            FROM DayMeals dm JOIN Meals m ON dm.MealId = m.Id
            WHERE dm.GroupId = @groupId
            ORDER BY dm.DayOfWeek";
        cmd.Parameters.AddWithValue("@groupId", groupId);

        var entries = new List<DayMealEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new DayMealEntry
            {
                DayOfWeek = reader.GetInt32(0),
                MealId = reader.GetInt32(1),
                MealName = reader.GetString(2),
            });
        }

        return entries;
    }

    /// <summary>
    /// Inserts or replaces a meal assignment for a specific day.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="dayOfWeek">The day of week (1=Monday, 7=Sunday).</param>
    /// <param name="mealId">The meal ID.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task UpsertAsync(int groupId, int dayOfWeek, int mealId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO DayMeals (GroupId, DayOfWeek, MealId) VALUES (@groupId, @dayOfWeek, @mealId)";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@dayOfWeek", dayOfWeek);
        cmd.Parameters.AddWithValue("@mealId", mealId);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Removes a meal assignment for a specific day.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="dayOfWeek">The day of week (1=Monday, 7=Sunday).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task ClearAsync(int groupId, int dayOfWeek)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM DayMeals WHERE GroupId = @groupId AND DayOfWeek = @dayOfWeek";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@dayOfWeek", dayOfWeek);

        await cmd.ExecuteNonQueryAsync();
    }
}
