// <copyright file="UseCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using System.Text;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /use command — shows all inventory items with expiry dates and remove buttons.
/// </summary>
public class UseCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseCommandHandler"/> class.
    /// </summary>
    public UseCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        PurchaseHistoryRepository purchaseRepository,
        ShoppingItemRepository itemRepository,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.purchaseRepository = purchaseRepository;
        this.itemRepository = itemRepository;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string Command => "/use";

    /// <inheritdoc/>
    public string? Description => "View current inventory";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var group = await this.groupRepository.GetOrCreateAsync(chatId);

        var phItems = await this.purchaseRepository.GetInventoryItemsWithExpiryAsync(group.Id);
        var siItems = await this.itemRepository.GetItemsWithExpiryAsync(group.Id);

        if (phItems.Count == 0 && siItems.Count == 0)
        {
            await this.botClient.SendMessage(
                chatId: chatId,
                text: this.localizer.Get(chatId, "use.empty"),
                cancellationToken: cancellationToken);
            return;
        }

        var removeIcon = this.localizer.Get(chatId, "use.remove-button");

        var entries = siItems
            .Select(i => (ExpDate: i.ExpDate!.Value, Label: FormatLabel(i.Name, i.Quantity, i.ExpDate!.Value), Callback: $"use:remove:si:{i.Id}"))
            .Concat(phItems.Select(i => (ExpDate: i.ExpDate!.Value, Label: FormatLabel(i.ItemName, i.Quantity, i.ExpDate!.Value), Callback: $"use:remove:ph:{i.Id}")))
            .OrderBy(e => e.ExpDate)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(this.localizer.Get(chatId, "use.header"));
        foreach (var e in entries)
        {
            sb.AppendLine($"• {e.Label}");
        }

        var rows = entries
            .Select(e => new[] { InlineKeyboardButton.WithCallbackData($"{removeIcon} {e.Label}", e.Callback) })
            .ToList();

        await this.botClient.SendMessage(
            chatId: chatId,
            text: sb.ToString(),
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);
    }

    internal static string FormatLabel(string name, string? quantity, DateOnly expDate)
    {
        var qty = quantity is not null ? $" {quantity}" : string.Empty;
        return $"{name}{qty} · {expDate:dd.MM.yyyy}";
    }
}
