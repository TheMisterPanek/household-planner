// <copyright file="MergeConflict.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents a conflict when merging meal ingredients into the shopping list.
/// </summary>
public class MergeConflict
{
    /// <summary>
    /// Gets or sets the ingredient name.
    /// </summary>
    public string IngredientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity needed for the meal (nullable).
    /// </summary>
    public string? MealQuantity { get; set; }

    /// <summary>
    /// Gets or sets the existing quantity on the shopping list (nullable).
    /// </summary>
    public string? ExistingQuantity { get; set; }
}
