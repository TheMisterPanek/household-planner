// <copyright file="UseRemoveCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the remove-item buttons in /use and expiry notification messages.
/// Callback format: use:remove:ph:{id} (purchase history) or use:remove:si:{id} (shopping item).
/// </summary>
public class UseRemoveCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<UseRemoveCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseRemoveCallbackHandler"/> class.
    /// </summary>
    public UseRemoveCallbackHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        PurchaseHistoryRepository purchaseRepository,
        ShoppingItemRepository itemRepository,
        ILocalizer localizer,
        ILogger<UseRemoveCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.purchaseRepository = purchaseRepository;
        this.itemRepository = itemRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "use:remove:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        // Format: use:remove:{ph|si}:{id}
        var parts = callbackQuery.Data.Split(':');
        if (parts.Length < 4 || !int.TryParse(parts[3], out var itemId))
        {
            this.logger.LogWarning("Invalid use:remove callback data: {Data}", callbackQuery.Data);
            return;
        }

        var source = parts[2]; // "ph" or "si"
        var chatId = callbackQuery.Message.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        if (source == "ph")
        {
            await this.purchaseRepository.DeleteAsync(itemId);
        }
        else if (source == "si")
        {
            await this.itemRepository.DeleteAsync(itemId);
        }
        else
        {
            this.logger.LogWarning("Unknown source in use:remove callback: {Source}", source);
            return;
        }

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: this.localizer.Get(chatId, "use.remove-toast"),
            cancellationToken: cancellationToken);

        // Rebuild keyboard with remaining items
        var group = await this.groupRepository.GetOrCreateAsync(chatId);
        var phItems = await this.purchaseRepository.GetInventoryItemsWithExpiryAsync(group.Id);
        var siItems = await this.itemRepository.GetItemsWithExpiryAsync(group.Id);

        var removeIcon = this.localizer.Get(chatId, "use.remove-button");

        var entries = siItems
            .Select(i => (ExpDate: i.ExpDate!.Value, Label: UseCommandHandler.FormatLabel(i.Name, i.Quantity, i.ExpDate!.Value), Callback: $"use:remove:si:{i.Id}"))
            .Concat(phItems.Select(i => (ExpDate: i.ExpDate!.Value, Label: UseCommandHandler.FormatLabel(i.ItemName, i.Quantity, i.ExpDate!.Value), Callback: $"use:remove:ph:{i.Id}")))
            .OrderBy(e => e.ExpDate)
            .ToList();

        if (entries.Count == 0)
        {
            await this.botClient.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: this.localizer.Get(chatId, "use.empty"),
                replyMarkup: null,
                cancellationToken: cancellationToken);
            return;
        }

        var rows = entries
            .Select(e => new[] { InlineKeyboardButton.WithCallbackData($"{removeIcon} {e.Label}", e.Callback) })
            .ToList();

        await this.botClient.EditMessageReplyMarkup(
            chatId: chatId,
            messageId: messageId,
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);
    }
}
