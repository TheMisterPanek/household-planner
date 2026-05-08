// <copyright file="ItemPayload.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Payload for item-related actions (add, buy, remove).
/// </summary>
public record ItemPayload(string Name, string? Quantity);
