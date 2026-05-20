// <copyright file="MealStepRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using Microsoft.Data.Sqlite;
using ProductTrackerBot.Models;

/// <summary>
/// Data access for the MealSteps table.
/// </summary>
public class MealStepRepository
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="MealStepRepository"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public MealStepRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Gets all steps for a meal.
    /// </summary>
    /// <param name="mealId">The meal ID.</param>
    /// <returns>A list of steps for the meal.</returns>
    public virtual async Task<List<MealStep>> GetAllAsync(int mealId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, MealId, StepNumber, Text FROM MealSteps WHERE MealId = @mealId ORDER BY StepNumber";
        cmd.Parameters.AddWithValue("@mealId", mealId);

        var steps = new List<MealStep>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            steps.Add(new MealStep
            {
                Id = reader.GetInt32(0),
                MealId = reader.GetInt32(1),
                StepNumber = reader.GetInt32(2),
                Text = reader.GetString(3),
            });
        }

        return steps;
    }

    /// <summary>
    /// Adds a new step to a meal (auto-assigns StepNumber = max+1).
    /// </summary>
    /// <param name="mealId">The meal ID.</param>
    /// <param name="text">The step text.</param>
    /// <returns>The created step.</returns>
    public virtual async Task<MealStep> AddAsync(int mealId, string text)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        // Get the next step number
        await using var maxCmd = connection.CreateCommand();
        maxCmd.CommandText = "SELECT COALESCE(MAX(StepNumber), 0) FROM MealSteps WHERE MealId = @mealId";
        maxCmd.Parameters.AddWithValue("@mealId", mealId);

        var maxStepNumber = (long)(await maxCmd.ExecuteScalarAsync())!;
        var nextStepNumber = (int)maxStepNumber + 1;

        await using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = "INSERT INTO MealSteps (MealId, StepNumber, Text) VALUES (@mealId, @stepNumber, @text); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("@mealId", mealId);
        insertCmd.Parameters.AddWithValue("@stepNumber", nextStepNumber);
        insertCmd.Parameters.AddWithValue("@text", text);

        var newId = (long)(await insertCmd.ExecuteScalarAsync())!;

        return new MealStep
        {
            Id = (int)newId,
            MealId = mealId,
            StepNumber = nextStepNumber,
            Text = text,
        };
    }

    /// <summary>
    /// Updates the text of a step.
    /// </summary>
    /// <param name="stepId">The step ID.</param>
    /// <param name="text">The new text.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task UpdateAsync(int stepId, string text)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE MealSteps SET Text = @text WHERE Id = @stepId";
        cmd.Parameters.AddWithValue("@text", text);
        cmd.Parameters.AddWithValue("@stepId", stepId);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Updates the step number (display order) of a step.
    /// </summary>
    /// <param name="stepId">The step ID.</param>
    /// <param name="stepNumber">The new step number.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task UpdateStepNumberAsync(int stepId, int stepNumber)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE MealSteps SET StepNumber = @stepNumber WHERE Id = @stepId";
        cmd.Parameters.AddWithValue("@stepNumber", stepNumber);
        cmd.Parameters.AddWithValue("@stepId", stepId);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Deletes a step.
    /// </summary>
    /// <param name="stepId">The step ID.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task DeleteAsync(int stepId)
    {
        await using var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM MealSteps WHERE Id = @stepId";
        cmd.Parameters.AddWithValue("@stepId", stepId);

        await cmd.ExecuteNonQueryAsync();
    }
}
