// <copyright file="ShoppingListService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Text;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Builds shopping list message text and inline keyboards.
/// </summary>
public class ShoppingListService
{
    private readonly GroupRepository groupRepository;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingListService"/> class.
    /// </summary>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public ShoppingListService(GroupRepository groupRepository, ShoppingItemRepository itemRepository, ILocalizer localizer)
    {
        this.groupRepository = groupRepository;
        this.itemRepository = itemRepository;
        this.localizer = localizer;
    }

    /// <summary>
    /// Gets paginated items with pagination metadata for a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="pageNumber">The page number (1-based; invalid numbers default to 1).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>A tuple of (items, totalItems, totalPages, actualPageNumber).</returns>
    public virtual async Task<(IReadOnlyList<ShoppingItem> Items, int TotalItems, int TotalPages, int ActualPageNumber)> GetPagedItemsAsync(
        int groupId,
        int pageNumber,
        int pageSize)
    {
        var items = await this.itemRepository.GetAllAsync(groupId);
        var totalItems = items.Count;
        var totalPages = (totalItems + pageSize - 1) / pageSize; // Ceiling division
        if (totalPages == 0)
        {
            totalPages = 1;
        }

        var actualPageNumber = pageNumber;
        if (actualPageNumber < 1 || actualPageNumber > totalPages)
        {
            actualPageNumber = 1;
        }

        var skip = (actualPageNumber - 1) * pageSize;
        var pagedItems = items.Skip(skip).Take(pageSize).ToList();

        return (pagedItems.AsReadOnly(), totalItems, totalPages, actualPageNumber);
    }

    /// <summary>
    /// Builds the shopping list message text and inline keyboard for a group chat.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="pageNumber">The page number for pagination (1-based; defaults to 1).</param>
    /// <returns>A tuple of (messageText, inlineKeyboard, group).</returns>
    public virtual async Task<(string MessageText, InlineKeyboardMarkup? Keyboard, Group Group)> BuildListAsync(
        long chatId,
        int pageNumber = 1)
    {
        const int pageSize = 10;
        var group = await this.groupRepository.GetOrCreateAsync(chatId);

        var (pagedItems, totalItems, totalPages, actualPageNumber) = await this.GetPagedItemsAsync(group.Id, pageNumber, pageSize);

        if (totalItems == 0)
        {
            return (this.localizer.Get(chatId, "list.empty"), null, group);
        }

        var sb = new StringBuilder(this.localizer.Get(chatId, "list.header") + "\n\n");
        var buttons = new List<List<InlineKeyboardButton>>();

        var groups = pagedItems.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var group2 in groups)
        {
            var groupItems = group2.ToList();
            var quantities = groupItems
                .Where(i => i.Quantity is not null)
                .Select(i => i.Quantity!);
            var label = groupItems[0].Name;
            if (quantities.Any())
            {
                label += " " + string.Join(", ", quantities);
            }

            var firstItem = groupItems[0];
            if (firstItem.ExpDate.HasValue)
            {
                label += $" (до {firstItem.ExpDate.Value:dd.MM})";
            }

            var isExpired = firstItem.ExpDate.HasValue && firstItem.ExpDate.Value <= DateOnly.FromDateTime(DateTime.Now);
            if (isExpired)
            {
                sb.Append("⚠️ ");
            }

            sb.AppendLine($"• {label}");

            foreach (var item in groupItems)
            {
                var btnLabel = item.Quantity is not null
                    ? $"✓ {item.Name} {item.Quantity}"
                    : $"✓ {item.Name}";

                if (item.ExpDate.HasValue)
                {
                    btnLabel += $" (до {item.ExpDate.Value:dd.MM})";
                }

                buttons.Add(
                [
                    InlineKeyboardButton.WithCallbackData(btnLabel, $"shop:done:{item.Id}"),
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "list.remove"), $"shop:remove:{item.Id}"),
                ]);
            }
        }

        // Add pagination buttons if there are multiple pages
        if (totalPages > 1)
        {
            var paginationButtons = new List<InlineKeyboardButton>();
            if (actualPageNumber > 1)
            {
                paginationButtons.Add(InlineKeyboardButton.WithCallbackData(
                    this.localizer.Get(chatId, "pagination_previous_button"),
                    $"list_prev:{group.ChatId}:{actualPageNumber - 1}"));
            }

            if (actualPageNumber < totalPages)
            {
                paginationButtons.Add(InlineKeyboardButton.WithCallbackData(
                    this.localizer.Get(chatId, "pagination_next_button"),
                    $"list_next:{group.ChatId}:{actualPageNumber + 1}"));
            }

            if (paginationButtons.Count > 0)
            {
                buttons.Add(paginationButtons);
            }
        }

        // Add page footer
        var pageLabel = this.localizer.Get(chatId, "pagination_page_label");
        var ofLabel = this.localizer.Get(chatId, "pagination_of_label");
        var itemsLabel = this.localizer.Get(chatId, "pagination_items_label");
        sb.AppendLine($"\n{pageLabel} {actualPageNumber} {ofLabel} {totalPages} ({totalItems} {itemsLabel})");

        return (sb.ToString(), new InlineKeyboardMarkup(buttons), group);
    }
}
