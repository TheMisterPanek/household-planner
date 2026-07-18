// <copyright file="TagCaptureDialogState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// State for the multi-select tag-capture follow-up dialog after an item is added or edited.
/// </summary>
public class TagCaptureDialogState
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
    /// Gets or sets the top tags suggested from purchase history for this prompt.
    /// </summary>
    public List<string>? TopTags { get; set; }

    /// <summary>
    /// Gets or sets the mutable, accumulating set of tag names currently selected in this dialog.
    /// </summary>
    public HashSet<string> SelectedTagNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
