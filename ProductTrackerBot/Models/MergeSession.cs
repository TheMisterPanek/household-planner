// <copyright file="MergeSession.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents an in-progress merge session for resolving ingredient conflicts.
/// </summary>
public class MergeSession
{
    /// <summary>
    /// Gets or sets the chat ID where the merge was initiated.
    /// </summary>
    public long ChatId { get; set; }

    /// <summary>
    /// Gets or sets the meal ID being added to the shopping list.
    /// </summary>
    public int MealId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the list of conflicts detected.
    /// </summary>
    public List<MergeConflict> Conflicts { get; set; } = new();

    /// <summary>
    /// Gets or sets the resolution map (index → wasAdded).
    /// </summary>
    public Dictionary<int, bool> Resolved { get; set; } = new();
}
