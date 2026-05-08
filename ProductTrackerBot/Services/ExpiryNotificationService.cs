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
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiryNotificationService"/> class.
    /// </summary>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public ExpiryNotificationService(ShoppingItemRepository itemRepository, ILocalizer localizer)
    {
        this.itemRepository = itemRepository;
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
        var items = await this.itemRepository.GetItemsWithExpiryAsync(groupId);

        var expired = items.Where(i => i.ExpDate < today).ToList();
        var expiringToday = items.Where(i => i.ExpDate == today).ToList();
        var expiringSoon = items.Where(i => today < i.ExpDate && i.ExpDate <= today.AddDays(3)).ToList();
        var expiringThisWeek = items.Where(i => today.AddDays(3) < i.ExpDate && i.ExpDate <= today.AddDays(7)).ToList();

        if (expired.Count == 0 && expiringToday.Count == 0 && expiringSoon.Count == 0 && expiringThisWeek.Count == 0)
        {
            return null;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📋 Сводка по срокам годности:");

        if (expired.Count > 0)
        {
            sb.AppendLine("\n🔴 Просроченные:");
            foreach (var item in expired)
            {
                sb.AppendLine($"• {item.Name}{(item.Quantity is not null ? $" {item.Quantity}" : string.Empty)}");
            }
        }

        if (expiringToday.Count > 0)
        {
            sb.AppendLine("\n🟡 Истекает сегодня:");
            foreach (var item in expiringToday)
            {
                sb.AppendLine($"• {item.Name}{(item.Quantity is not null ? $" {item.Quantity}" : string.Empty)} ({item.ExpDate!.Value:dd.MM.yyyy})");
            }
        }

        if (expiringSoon.Count > 0)
        {
            sb.AppendLine("\n🟠 Истекает скоро (до 3 дней):");
            foreach (var item in expiringSoon)
            {
                sb.AppendLine($"• {item.Name}{(item.Quantity is not null ? $" {item.Quantity}" : string.Empty)} ({item.ExpDate!.Value:dd.MM.yyyy})");
            }
        }

        if (expiringThisWeek.Count > 0)
        {
            sb.AppendLine("\n📅 Истекает на этой неделе (4–7 дней):");
            foreach (var item in expiringThisWeek)
            {
                sb.AppendLine($"• {item.Name}{(item.Quantity is not null ? $" {item.Quantity}" : string.Empty)} ({item.ExpDate!.Value:dd.MM.yyyy})");
            }
        }

        return sb.ToString();
    }
}
