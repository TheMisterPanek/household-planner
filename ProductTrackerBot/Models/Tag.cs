// <copyright file="Tag.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents a group-scoped tag that can be attached to shopping items and purchase history records.
/// </summary>
public record Tag
{
    /// <summary>
    /// Gets the database primary key.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the foreign key referencing the owning group.
    /// </summary>
    public int GroupId { get; init; }

    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
