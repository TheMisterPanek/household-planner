// <copyright file="PurchaseRecord.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents a completed purchase record in the PurchaseHistory table.
/// </summary>
public class PurchaseRecord
{
    /// <summary>
    /// Gets or sets the database primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the foreign key referencing the group.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional quantity (e.g. "2л", "1 шт").
    /// </summary>
    public string? Quantity { get; set; }

    /// <summary>
    /// Gets or sets the optional store name.
    /// </summary>
    public string? StoreName { get; set; }

    /// <summary>
    /// Gets or sets the optional price as a decimal.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Gets or sets the purchase timestamp (ISO 8601).
    /// </summary>
    public DateTime PurchasedAt { get; set; }

    /// <summary>
    /// Gets or sets the display name of the buyer.
    /// </summary>
    public string BoughtByName { get; set; } = string.Empty;
}
