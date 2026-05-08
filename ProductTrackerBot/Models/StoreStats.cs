// <copyright file="StoreStats.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Price statistics for a store.
/// </summary>
/// <param name="StoreName">The name of the store (null for unknown stores).</param>
/// <param name="Min">The minimum price for the item at this store.</param>
/// <param name="Avg">The average price for the item at this store.</param>
/// <param name="Max">The maximum price for the item at this store.</param>
/// <param name="Count">The number of observations.</param>
public record StoreStats(
    string? StoreName,
    decimal Min,
    decimal Avg,
    decimal Max,
    int Count);
