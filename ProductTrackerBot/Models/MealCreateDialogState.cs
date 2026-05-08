// <copyright file="MealCreateDialogState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents the state of a meal creation dialog.
/// </summary>
public class MealCreateDialogState
{
    /// <summary>
    /// Gets or sets the dialog step (always 1 — awaiting meal name).
    /// </summary>
    public int Step { get; set; } = 1;
}
