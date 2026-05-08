// <copyright file="Meal.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents a meal.
/// </summary>
public record Meal
{
    /// <summary>
    /// Gets the database primary key.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the foreign key referencing the group.
    /// </summary>
    public int GroupId { get; init; }

    /// <summary>
    /// Gets the meal name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
