// <copyright file="MealIngredient.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents an ingredient in a meal.
/// </summary>
public record MealIngredient
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
    /// Gets the ingredient name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional quantity (e.g. "2л", "1 шт").
    /// </summary>
    public string? Quantity { get; init; }
}
