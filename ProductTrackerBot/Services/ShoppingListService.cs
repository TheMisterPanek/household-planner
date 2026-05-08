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
    /// Builds the shopping list message text and inline keyboard for a group chat.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <returns>A tuple of (messageText, inlineKeyboard, group).</returns>
    public virtual async Task<(string MessageText, InlineKeyboardMarkup? Keyboard, Group Group)> BuildListAsync(long chatId)
    {
        var group = await this.groupRepository.GetOrCreateAsync(chatId);
        var items = await this.itemRepository.GetAllAsync(group.Id);

        if (items.Count == 0)
        {
            return (this.localizer.Get(chatId, "list.empty"), null, group);
        }

        var sb = new StringBuilder(this.localizer.Get(chatId, "list.header") + "\n\n");
        var buttons = new List<List<InlineKeyboardButton>>();

        var groups = items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var group2 in groups)
        {
            var groupItems = group2.ToList();
            var quantities = groupItems
                .Where(i => i.Quantity is not null)
                .Select(i => i.Quantity!);
            var label = groupItems[0].Name;
            if (quantities.Any())
                label += " " + string.Join(", ", quantities);
            sb.AppendLine($"• {label}");

            foreach (var item in groupItems)
            {
                var btnLabel = item.Quantity is not null
                    ? $"✓ {item.Name} {item.Quantity}"
                    : $"✓ {item.Name}";
                buttons.Add(
                [
                    InlineKeyboardButton.WithCallbackData(btnLabel, $"shop:done:{item.Id}"),
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "list.remove"), $"shop:remove:{item.Id}"),
                ]);
            }
        }

        return (sb.ToString(), new InlineKeyboardMarkup(buttons), group);
    }
}
