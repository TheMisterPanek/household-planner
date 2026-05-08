// <copyright file="ListViewedPayload.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Payload for list viewing with pagination metadata.
/// </summary>
public record ListViewedPayload(int Page, int PageSize, int TotalItems);
