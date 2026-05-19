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
    /// Gets all day-meal entries for a group's weekly plan, scoped to a specific calendar week.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="weekStartDate">The ISO-8601 date of the week's Monday (yyyy-MM-dd).</param>
    /// <returns>A list of day-meal entries ordered by day of week.</returns>
    public virtual async Task<List<DayMealEntry>> GetWeekAsync(int groupId, string weekStartDate)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT dm.DayOfWeek, dm.MealId, m.Name
            FROM DayMeals dm JOIN Meals m ON dm.MealId = m.Id
            WHERE dm.GroupId = @groupId AND dm.WeekStartDate = @weekStartDate
            ORDER BY dm.DayOfWeek, dm.Id";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@weekStartDate", weekStartDate);

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
    /// Inserts a meal assignment for a specific day in a specific calendar week.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="dayOfWeek">The day of week (1=Monday, 7=Sunday).</param>
    /// <param name="mealId">The meal ID.</param>
    /// <param name="weekStartDate">The ISO-8601 date of the week's Monday (yyyy-MM-dd).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task InsertAsync(int groupId, int dayOfWeek, int mealId, string weekStartDate)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO DayMeals (GroupId, DayOfWeek, MealId, WeekStartDate) VALUES (@groupId, @dayOfWeek, @mealId, @weekStartDate)";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@dayOfWeek", dayOfWeek);
        cmd.Parameters.AddWithValue("@mealId", mealId);
        cmd.Parameters.AddWithValue("@weekStartDate", weekStartDate);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Counts the number of meals assigned to a specific day in a specific calendar week.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="dayOfWeek">The day of week (1=Monday, 7=Sunday).</param>
    /// <param name="weekStartDate">The ISO-8601 date of the week's Monday (yyyy-MM-dd).</param>
    /// <returns>The count of meals for that day.</returns>
    public virtual async Task<int> GetCountAsync(int groupId, int dayOfWeek, string weekStartDate)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM DayMeals WHERE GroupId = @groupId AND DayOfWeek = @dayOfWeek AND WeekStartDate = @weekStartDate";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@dayOfWeek", dayOfWeek);
        cmd.Parameters.AddWithValue("@weekStartDate", weekStartDate);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>
    /// Removes all meal assignments for a specific day in a specific calendar week.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="dayOfWeek">The day of week (1=Monday, 7=Sunday).</param>
    /// <param name="weekStartDate">The ISO-8601 date of the week's Monday (yyyy-MM-dd).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task ClearAsync(int groupId, int dayOfWeek, string weekStartDate)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM DayMeals WHERE GroupId = @groupId AND DayOfWeek = @dayOfWeek AND WeekStartDate = @weekStartDate";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@dayOfWeek", dayOfWeek);
        cmd.Parameters.AddWithValue("@weekStartDate", weekStartDate);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Removes a specific meal from a specific day in a specific calendar week.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="dayOfWeek">The day of week (1=Monday, 7=Sunday).</param>
    /// <param name="mealId">The meal ID to remove.</param>
    /// <param name="weekStartDate">The ISO-8601 date of the week's Monday (yyyy-MM-dd).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task ClearMealAsync(int groupId, int dayOfWeek, int mealId, string weekStartDate)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM DayMeals WHERE GroupId = @groupId AND DayOfWeek = @dayOfWeek AND MealId = @mealId AND WeekStartDate = @weekStartDate";
        cmd.Parameters.AddWithValue("@groupId", groupId);
        cmd.Parameters.AddWithValue("@dayOfWeek", dayOfWeek);
        cmd.Parameters.AddWithValue("@mealId", mealId);
        cmd.Parameters.AddWithValue("@weekStartDate", weekStartDate);

        await cmd.ExecuteNonQueryAsync();
    }
}
