// <copyright file="PriceCaptureDialogState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// State for the optional price-capture dialog after marking an item as bought.
/// </summary>
public class PriceCaptureDialogState
{
    /// <summary>
    /// Gets or sets the current dialog step.
    /// 1 = awaiting store name, 2 = awaiting price, 3 = awaiting expiry date.
    /// </summary>
    public int Step { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional quantity.
    /// </summary>
    public string? Quantity { get; set; }

    /// <summary>
    /// Gets or sets the display name of the buyer.
    /// </summary>
    public string BoughtByName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the group ID.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Gets or sets the optional store name (filled at step 1).
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// Gets or sets the top shops suggested from purchase history for step 1.
    /// </summary>
    public List<string>? TopShops { get; set; }

    /// <summary>
    /// Gets or sets the optional price (filled at step 2).
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Gets or sets the optional expiry date of the purchased item (filled at step 3).
    /// </summary>
    public DateOnly? ExpDate { get; set; }

    /// <summary>
    /// Gets or sets the optional category carried over from the shopping item, saved to the purchase record.
    /// </summary>
    public string? Category { get; set; }
}
