// <copyright file="ExpiryNotificationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;

/// <summary>
/// Builds expiry notification messages for shopping items approaching or past expiry dates.
/// </summary>
public class ExpiryNotificationService
{
    private readonly ShoppingItemRepository itemRepository;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiryNotificationService"/> class.
    /// </summary>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public ExpiryNotificationService(ShoppingItemRepository itemRepository, PurchaseHistoryRepository purchaseRepository, ILocalizer localizer)
    {
        this.itemRepository = itemRepository;
        this.purchaseRepository = purchaseRepository;
        this.localizer = localizer;
    }

    /// <summary>
    /// Builds a summary message for items with upcoming or past expiry dates, or null if nothing to report.
    /// </summary>
    /// <param name="chatId">The chat ID for localization.</param>
    /// <param name="groupId">The group ID to query.</param>
    /// <param name="today">The current date (for testing, usually DateOnly.FromDateTime(DateTime.Now)).</param>
    /// <returns>The formatted summary message or null if no items need reporting.</returns>
    public virtual async Task<string?> BuildSummaryAsync(long chatId, int groupId, DateOnly today)
    {
        var plannedItems = await this.itemRepository.GetItemsWithExpiryAsync(groupId);
        var boughtItems = await this.purchaseRepository.GetItemsWithExpiryAsync(groupId);

        var mergedItems = new List<(string Name, string? Quantity, DateOnly ExpDate)>();
        mergedItems.AddRange(plannedItems.Select(p => (Name: p.Name, Quantity: p.Quantity, ExpDate: p.ExpDate!.Value)));
        mergedItems.AddRange(boughtItems.Select(b => (Name: b.ItemName, Quantity: b.Quantity, ExpDate: b.ExpDate)));

        var expired = mergedItems.Where(i => i.ExpDate < today).ToList();
        var expiringToday = mergedItems.Where(i => i.ExpDate == today).ToList();
        var expiringSoon = mergedItems.Where(i => today < i.ExpDate && i.ExpDate <= today.AddDays(3)).ToList();
        var expiringThisWeek = mergedItems.Where(i => today.AddDays(3) < i.ExpDate && i.ExpDate <= today.AddDays(7)).ToList();

        if (expired.Count == 0 && expiringToday.Count == 0 && expiringSoon.Count == 0 && expiringThisWeek.Count == 0)
        {
            return null;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(this.localizer.Get(chatId, "notify.header"));

        if (expired.Count > 0)
        {
            sb.AppendLine($"\n{this.localizer.Get(chatId, "notify.section-expired")}");
            foreach (var item in expired)
            {
                sb.AppendLine($"• {item.Name}{(item.Quantity is not null ? $" {item.Quantity}" : string.Empty)}");
            }
        }

        if (expiringToday.Count > 0)
        {
            sb.AppendLine($"\n{this.localizer.Get(chatId, "notify.section-today")}");
            foreach (var item in expiringToday)
            {
                sb.AppendLine($"• {item.Name}{(item.Quantity is not null ? $" {item.Quantity}" : string.Empty)} ({item.ExpDate:dd.MM.yyyy})");
            }
        }

        if (expiringSoon.Count > 0)
        {
            sb.AppendLine($"\n{this.localizer.Get(chatId, "notify.section-soon")}");
            foreach (var item in expiringSoon)
            {
                sb.AppendLine($"• {item.Name}{(item.Quantity is not null ? $" {item.Quantity}" : string.Empty)} ({item.ExpDate:dd.MM.yyyy})");
            }
        }

        if (expiringThisWeek.Count > 0)
        {
            sb.AppendLine($"\n{this.localizer.Get(chatId, "notify.section-week")}");
            foreach (var item in expiringThisWeek)
            {
                sb.AppendLine($"• {item.Name}{(item.Quantity is not null ? $" {item.Quantity}" : string.Empty)} ({item.ExpDate:dd.MM.yyyy})");
            }
        }

        return sb.ToString();
    }
}
