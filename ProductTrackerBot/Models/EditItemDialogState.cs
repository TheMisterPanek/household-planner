// <copyright file="EditItemDialogState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// State for the item-edit one-step dialog triggered by the ✏️ button in the list view.
/// </summary>
public class EditItemDialogState
{
    /// <summary>Gets or sets the ID of the item being edited.</summary>
    public int ItemId { get; set; }

    /// <summary>Gets or sets the group ID of the item.</summary>
    public int GroupId { get; set; }

    /// <summary>Gets or sets the item's original name (shown in the edit prompt).</summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>Gets or sets the item's original quantity (shown in the edit prompt).</summary>
    public string? OriginalQuantity { get; set; }
}
