// <copyright file="DayMealEntry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents a meal assigned to a day of the week in the group's plan.
/// </summary>
public record DayMealEntry
{
    /// <summary>
    /// Gets the day of week (1=Monday, 7=Sunday).
    /// </summary>
    public int DayOfWeek { get; init; }

    /// <summary>
    /// Gets the meal ID.
    /// </summary>
    public int MealId { get; init; }

    /// <summary>
    /// Gets the meal name (joined from Meals table).
    /// </summary>
    public string MealName { get; init; } = string.Empty;
}
