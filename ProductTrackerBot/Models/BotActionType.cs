// <copyright file="BotActionType.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Identifies the kind of action recorded in <c>BotActionHistory</c>.
/// </summary>
public enum BotActionType
{
    /// <summary>An item was added to the shopping list.</summary>
    ItemAdded,

    /// <summary>An item was marked as bought and removed.</summary>
    ItemBought,

    /// <summary>An item was removed from the list without buying.</summary>
    ItemRemoved,

    /// <summary>The shopping list was viewed.</summary>
    ListViewed,

    /// <summary>The action history was viewed.</summary>
    HistoryViewed,

    /// <summary>The language preference was changed.</summary>
    LanguageChanged,

    /// <summary>The shopping list was archived.</summary>
    ListArchived,
}
