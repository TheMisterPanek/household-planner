// <copyright file="ShoppingItem.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents a shopping list item.
/// </summary>
public record ShoppingItem
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
    /// Gets the item name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional quantity (e.g. "2л", "1 шт").
    /// </summary>
    public string? Quantity { get; init; }

    /// <summary>
    /// Gets the optional expiry date.
    /// </summary>
    public DateOnly? ExpDate { get; init; }

    /// <summary>
    /// Gets the display name of the user who added the item.
    /// </summary>
    public string AddedByName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tags currently attached to this item (e.g. "Бытовая химия", "Скидка").
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
