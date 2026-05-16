// <copyright file="PendingAddItem.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Holds an in-progress "add item" pending user confirmation.
/// </summary>
public record PendingAddItem(long ChatId, int GroupId, string Name, string? Quantity, string AddedByName);
