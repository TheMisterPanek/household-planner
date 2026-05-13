// <copyright file="RevertOperation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Base type for all revert operation descriptors stored in <c>revert_payload</c>.
/// </summary>
public abstract record RevertOperation;

/// <summary>
/// Revert payload for an item that was marked as bought (deleted from the list).
/// </summary>
public record ItemBoughtRevert(int ItemId, string ItemName, string? Quantity, int ListId) : RevertOperation;

/// <summary>
/// Revert payload for an item that was removed from the list without buying.
/// </summary>
public record ItemRemovedRevert(string ItemName, string? Quantity, int ListId) : RevertOperation;

/// <summary>
/// Revert payload for a shopping list that was archived.
/// </summary>
public record ListArchivedRevert(int ListId) : RevertOperation;
