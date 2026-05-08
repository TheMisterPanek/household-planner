// <copyright file="MealAddIngredientDialogState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents the state of a meal ingredient addition dialog.
/// </summary>
public class MealAddIngredientDialogState
{
    /// <summary>
    /// Gets or sets the dialog step (1 = name, 2 = quantity).
    /// </summary>
    public int Step { get; set; } = 1;

    /// <summary>
    /// Gets or sets the meal ID.
    /// </summary>
    public int MealId { get; set; }

    /// <summary>
    /// Gets or sets the ingredient name (populated after step 1).
    /// </summary>
    public string? Name { get; set; }
}
