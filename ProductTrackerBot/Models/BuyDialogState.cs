// <copyright file="BuyDialogState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// State for the /buy multi-step dialog.
/// </summary>
public class BuyDialogState
{
    /// <summary>
    /// Gets or sets the current dialog step.
    /// 1 = waiting for item name, 2 = waiting for quantity.
    /// (Expiry date is captured in the price-capture dialog when marking as bought, not in /buy)
    /// </summary>
    public int Step { get; set; }

    /// <summary>
    /// Gets or sets the group ID for the shopping list.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Gets or sets the item name entered by the user.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the quantity entered by the user.
    /// </summary>
    public string? Quantity { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user who started /buy.
    /// </summary>
    public string AddedByName { get; set; } = string.Empty;
}
