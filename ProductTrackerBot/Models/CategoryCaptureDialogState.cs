// <copyright file="CategoryCaptureDialogState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// State for the category-capture follow-up dialog after an item is added or edited.
/// </summary>
public class CategoryCaptureDialogState
{
    /// <summary>
    /// Gets or sets the IDs of the shopping item(s) this prompt applies to.
    /// </summary>
    public List<int> ItemIds { get; set; } = new();

    /// <summary>
    /// Gets or sets the display text for the prompt (item name, or "N товаров" for bulk).
    /// </summary>
    public string ItemLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the group ID.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Gets or sets the top categories suggested from purchase history for this prompt.
    /// </summary>
    public List<string>? TopCategories { get; set; }
}
