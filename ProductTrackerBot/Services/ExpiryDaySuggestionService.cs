// <copyright file="ExpiryDaySuggestionService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using ProductTrackerBot.Repositories;

/// <summary>
/// Returns smart shelf-life day suggestions for an item based on purchase history.
/// Tries similar-item history first; falls back to the group-wide average.
/// </summary>
public class ExpiryDaySuggestionService
{
    private readonly PurchaseHistoryRepository purchaseRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiryDaySuggestionService"/> class.
    /// </summary>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    public ExpiryDaySuggestionService(PurchaseHistoryRepository purchaseRepository)
    {
        this.purchaseRepository = purchaseRepository;
    }

    /// <summary>
    /// Returns up to 3 suggested shelf-life day counts for <paramref name="itemName"/> in the group.
    /// Falls back to the group-wide average when no similar-item records exist.
    /// Returns an empty list when the group has no expiry history at all.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="itemName">The item name to look up.</param>
    /// <returns>A read-only list of suggested day counts (0–3 items).</returns>
    public async Task<IReadOnlyList<int>> GetSuggestionsAsync(int groupId, string itemName)
    {
        var keyword = ExtractKeyword(itemName);
        var similar = await this.purchaseRepository.GetExpiryDaySuggestionsAsync(groupId, keyword);
        if (similar.Count > 0)
        {
            return similar.Take(3).ToList().AsReadOnly();
        }

        var avg = await this.purchaseRepository.GetAverageExpiryDaysAsync(groupId);
        if (avg > 0)
        {
            return new[] { avg }.AsReadOnly();
        }

        return Array.Empty<int>().AsReadOnly();
    }

    private static string ExtractKeyword(string itemName)
    {
        var first = itemName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? itemName;
        return first.ToLowerInvariant();
    }
}
