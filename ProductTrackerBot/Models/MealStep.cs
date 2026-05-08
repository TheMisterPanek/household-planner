// <copyright file="MealStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents a cooking step in a meal.
/// </summary>
public record MealStep
{
    /// <summary>
    /// Gets the database primary key.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the foreign key referencing the meal.
    /// </summary>
    public int MealId { get; init; }

    /// <summary>
    /// Gets the step number (display order).
    /// </summary>
    public int StepNumber { get; init; }

    /// <summary>
    /// Gets the step text.
    /// </summary>
    public string Text { get; init; } = string.Empty;
}
