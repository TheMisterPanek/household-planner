// <copyright file="PriceStats.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Price statistics for an item across all stores.
/// </summary>
/// <param name="Min">The minimum price for the item across all stores.</param>
/// <param name="Avg">The average price for the item across all stores.</param>
/// <param name="Max">The maximum price for the item across all stores.</param>
/// <param name="Count">The total number of price observations.</param>
/// <param name="StoreBreakdown">Per-store statistics, limited to top 10 stores by count.</param>
public record PriceStats(
    decimal Min,
    decimal Avg,
    decimal Max,
    int Count,
    IReadOnlyList<StoreStats> StoreBreakdown);
