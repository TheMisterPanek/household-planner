// <copyright file="BoughtDialogState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// State for the /bought multi-step dialog.
/// Step 1 = waiting for item name; Step 2 = waiting for expiry date.
/// </summary>
public class BoughtDialogState
{
    /// <summary>
    /// Gets or sets the current dialog step.
    /// </summary>
    public int Step { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional quantity (e.g. "1L", "12").
    /// </summary>
    public string? Quantity { get; set; }

    /// <summary>
    /// Gets or sets the group ID.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the buyer.
    /// </summary>
    public string BoughtByName { get; set; } = string.Empty;
}
