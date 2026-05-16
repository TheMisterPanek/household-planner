// <copyright file="PendingEditItem.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Holds an in-progress "edit item" pending user confirmation.
/// </summary>
public record PendingEditItem(long ChatId, int ItemId, int GroupId, string Name, string? Quantity);
