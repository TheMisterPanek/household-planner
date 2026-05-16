// <copyright file="ItemSaveCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "✓ Save" button from an item-edit review message.
/// </summary>
public class ItemSaveCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingEditService pendingEditService;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ShoppingListService listService;
    private readonly IHistoryRepository historyRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<ItemSaveCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemSaveCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="pendingEditService">The pending edit session service.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="listService">The shopping list service.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="logger">The logger.</param>
    public ItemSaveCallbackHandler(
        ITelegramBotClient botClient,
        PendingEditService pendingEditService,
        ShoppingItemRepository itemRepository,
        ShoppingListService listService,
        IHistoryRepository historyRepository,
        ILocalizer localizer,
        ILogger<ItemSaveCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.pendingEditService = pendingEditService;
        this.itemRepository = itemRepository;
        this.listService = listService;
        this.historyRepository = historyRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "item:save:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var token = callbackQuery.Data["item:save:".Length..];
        var pending = this.pendingEditService.Get(token);

        if (pending is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Session expired.",
                cancellationToken: cancellationToken);
            return;
        }

        this.pendingEditService.Clear(token);

        await this.itemRepository.UpdateAsync(pending.ItemId, pending.Name, pending.Quantity);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        var confirmText = pending.Quantity is not null
            ? this.localizer.Get(pending.ChatId, "item.saved-qty")
                .Replace("{item}", pending.Name).Replace("{quantity}", pending.Quantity)
            : this.localizer.Get(pending.ChatId, "item.saved")
                .Replace("{item}", pending.Name);

        await this.botClient.SendMessage(
            chatId: pending.ChatId,
            text: confirmText,
            cancellationToken: cancellationToken);

        // Refresh the list message
        var (messageText, keyboard, _) = await this.listService.BuildListAsync(pending.ChatId);
        try
        {
            await this.botClient.SendMessage(
                chatId: pending.ChatId,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to refresh list after item save");
        }

        try
        {
            var payload = new ItemPayload(pending.Name, pending.Quantity);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            await this.historyRepository.RecordAsync(
                chatId: pending.ChatId,
                userId: callbackQuery.From.Id,
                userName: callbackQuery.From.FirstName ?? "Unknown",
                actionType: BotActionType.ItemEdited,
                payloadJson: payloadJson,
                revertPayloadJson: null,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ItemEdited");
        }
    }
}
