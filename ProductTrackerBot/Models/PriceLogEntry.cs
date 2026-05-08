// <copyright file="PriceLogEntry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// A single price log entry for an item.
/// </summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="GroupId">The group this entry belongs to.</param>
/// <param name="ItemName">The name of the item.</param>
/// <param name="Price">The price paid for the item.</param>
/// <param name="StoreName">The store name where the item was purchased (optional).</param>
/// <param name="LoggedAt">The date and time the price was logged.</param>
public record PriceLogEntry(
    int Id,
    int GroupId,
    string ItemName,
    decimal Price,
    string? StoreName,
    DateTime LoggedAt);
