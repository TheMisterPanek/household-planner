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
    private const int BulkItemLimit = 20;

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
    /// Parses a comma-separated bulk input string and adds each item to the shopping list.
    /// Trims whitespace, filters empty segments, and enforces a 20-item cap.
    /// </summary>
    /// <param name="rawCsv">The raw user input (e.g. "Молоко 2л, Яйца 6, Хлеб").</param>
    /// <param name="groupId">The owning group ID.</param>
    /// <param name="addedByName">The display name of the user adding the items.</param>
    /// <returns>The list of added shopping items.</returns>
    public virtual async Task<IReadOnlyList<ShoppingItem>> AddItemsAsync(string rawCsv, int groupId, string addedByName)
    {
        var segments = rawCsv
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Take(BulkItemLimit)
            .ToList();

        var added = new List<ShoppingItem>(segments.Count);
        foreach (var segment in segments)
        {
            var (name, quantity) = BuyInputParser.Parse(segment);
            var item = await this.itemRepository.AddAsync(groupId, name, quantity, addedByName, expDate: null);
            added.Add(item);
        }

        return added.AsReadOnly();
    }

    /// <summary>
    /// Gets paginated items with pagination metadata for a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="pageNumber">The page number (1-based; invalid numbers default to 1).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="category">Optional category filter (case-insensitive exact match); null = unfiltered.</param>
    /// <returns>A tuple of (items, totalItems, totalPages, actualPageNumber).</returns>
    public virtual async Task<(IReadOnlyList<ShoppingItem> Items, int TotalItems, int TotalPages, int ActualPageNumber)> GetPagedItemsAsync(
        int groupId,
        int pageNumber,
        int pageSize,
        string? category = null)
    {
        var items = await this.itemRepository.GetAllAsync(groupId);
        if (category is not null)
        {
            items = items.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

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
    /// <param name="category">Optional category filter (case-insensitive exact match); null = unfiltered.</param>
    /// <returns>A tuple of (messageText, inlineKeyboard, group).</returns>
    public virtual async Task<(string MessageText, InlineKeyboardMarkup? Keyboard, Group Group)> BuildListAsync(
        long chatId,
        int pageNumber = 1,
        string? category = null)
    {
        const int pageSize = 10;
        var group = await this.groupRepository.GetOrCreateAsync(chatId);

        var (pagedItems, totalItems, totalPages, actualPageNumber) = await this.GetPagedItemsAsync(group.Id, pageNumber, pageSize, category);

        if (totalItems == 0)
        {
            if (category is not null)
            {
                // Filtered to a category with no current matches — still offer a way back to the full list.
                var allCategories = await this.itemRepository.GetDistinctCategoriesAsync(group.Id) ?? Array.Empty<string>();
                var emptyFilterButtons = new List<InlineKeyboardButton>();
                var emptyVisibleCount = Math.Min(allCategories.Count, 5);
                for (int i = 0; i < emptyVisibleCount; i++)
                {
                    emptyFilterButtons.Add(InlineKeyboardButton.WithCallbackData(
                        TruncateCategoryLabel(allCategories[i]),
                        $"list_filter:{group.ChatId}:{i}:1"));
                }

                emptyFilterButtons.Add(InlineKeyboardButton.WithCallbackData(
                    this.localizer.Get(chatId, "list.filter-all"),
                    $"list_filter:{group.ChatId}:-1:1"));

                var emptyKeyboard = new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>> { emptyFilterButtons });
                return (this.localizer.Get(chatId, "list.empty"), emptyKeyboard, group);
            }

            return (this.localizer.Get(chatId, "list.empty"), null, group);
        }

        var allItems = await this.itemRepository.GetAllAsync(group.Id);
        if (category is not null)
        {
            allItems = allItems.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sb = new StringBuilder(this.localizer.Get(chatId, "list.header") + "\n\n");
        sb.Append(FormatItemsAsText(allItems));

        var buttons = new List<List<InlineKeyboardButton>>();
        foreach (var itemGroup in pagedItems.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var item in itemGroup)
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
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "item.edit-button"), $"item:edit:{item.Id}"),
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "list.remove"), $"shop:remove:{item.Id}"),
                ]);
            }
        }

        var categories = await this.itemRepository.GetDistinctCategoriesAsync(group.Id) ?? Array.Empty<string>();
        var categoryIndex = category is not null
            ? IndexOfCategory(categories, category)
            : -1;

        // Add pagination buttons if there are multiple pages
        if (totalPages > 1)
        {
            var paginationButtons = new List<InlineKeyboardButton>();
            if (actualPageNumber > 1)
            {
                var callbackData = category is not null
                    ? $"list_filter:{group.ChatId}:{categoryIndex}:{actualPageNumber - 1}"
                    : $"list_prev:{group.ChatId}:{actualPageNumber - 1}";
                paginationButtons.Add(InlineKeyboardButton.WithCallbackData(
                    this.localizer.Get(chatId, "pagination_previous_button"),
                    callbackData));
            }

            if (actualPageNumber < totalPages)
            {
                var callbackData = category is not null
                    ? $"list_filter:{group.ChatId}:{categoryIndex}:{actualPageNumber + 1}"
                    : $"list_next:{group.ChatId}:{actualPageNumber + 1}";
                paginationButtons.Add(InlineKeyboardButton.WithCallbackData(
                    this.localizer.Get(chatId, "pagination_next_button"),
                    callbackData));
            }

            if (paginationButtons.Count > 0)
            {
                buttons.Add(paginationButtons);
            }
        }

        // Add category filter row (only when the group has at least one categorized active item)
        if (categories.Count > 0)
        {
            var filterButtons = new List<InlineKeyboardButton>();
            var visibleCount = Math.Min(categories.Count, 5);
            for (int i = 0; i < visibleCount; i++)
            {
                filterButtons.Add(InlineKeyboardButton.WithCallbackData(
                    TruncateCategoryLabel(categories[i]),
                    $"list_filter:{group.ChatId}:{i}:1"));
            }

            if (category is not null)
            {
                filterButtons.Add(InlineKeyboardButton.WithCallbackData(
                    this.localizer.Get(chatId, "list.filter-all"),
                    $"list_filter:{group.ChatId}:-1:1"));
            }

            buttons.Add(filterButtons);
        }

        // Add page footer
        var pageLabel = this.localizer.Get(chatId, "pagination_page_label");
        var ofLabel = this.localizer.Get(chatId, "pagination_of_label");
        var itemsLabel = this.localizer.Get(chatId, "pagination_items_label");
        sb.AppendLine($"\n{pageLabel} {actualPageNumber} {ofLabel} {totalPages} ({totalItems} {itemsLabel})");

        // Add Cancel button as the last row
        buttons.Add(
        [
            InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "action.cancel"), "action:cancel"),
        ]);

        return (sb.ToString(), new InlineKeyboardMarkup(buttons), group);
    }

    /// <summary>
    /// Finds the position of a category (case-insensitive) in a distinct-categories list, or -1 if not found
    /// (e.g. the category was re-tagged or removed since the message was last rendered).
    /// </summary>
    /// <param name="categories">The group's current distinct categories.</param>
    /// <param name="category">The category to locate.</param>
    /// <returns>The zero-based index, or -1 if not present.</returns>
    public static int IndexOfCategory(IReadOnlyList<string> categories, string category)
    {
        for (int i = 0; i < categories.Count; i++)
        {
            if (string.Equals(categories[i], category, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Truncates a category name for display on a button label. The full, untruncated name must always
    /// be the value persisted to storage — truncation is a rendering concern only.
    /// </summary>
    /// <param name="category">The full category name.</param>
    /// <returns>The truncated label.</returns>
    internal static string TruncateCategoryLabel(string category) =>
        category.Length > 20 ? category[..20] + "…" : category;

    private static string FormatItemsAsText(IReadOnlyList<ShoppingItem> items)
    {
        var sb = new StringBuilder();
        foreach (var itemGroup in items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
        {
            var groupItems = itemGroup.ToList();
            var quantities = groupItems
                .Where(i => i.Quantity is not null)
                .Select(i => i.Quantity!);
            var label = groupItems[0].Name;
            if (quantities.Any())
            {
                label += $" ({string.Join(", ", quantities)})";
            }

            var firstItem = groupItems[0];
            if (firstItem.ExpDate.HasValue)
            {
                label += $" (до {firstItem.ExpDate.Value:dd.MM})";
            }

            if (firstItem.ExpDate.HasValue && firstItem.ExpDate.Value <= DateOnly.FromDateTime(DateTime.Now))
            {
                sb.Append("⚠️ ");
            }

            sb.AppendLine($"• {label}");
        }

        return sb.ToString();
    }
}
