// <copyright file="BuySkipExpiryCallbackHandler.cs" company="PlaceholderCompany">
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
/// Handles the "Пропустить" callback during /buy expiry date step — saves item without expiry date.
/// </summary>
public class BuySkipExpiryCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly ShoppingItemRepository itemRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<BuySkipExpiryCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuySkipExpiryCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The dialog state service.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public BuySkipExpiryCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<BuyDialogState> dialogService,
        ShoppingItemRepository itemRepository,
        IHistoryRepository historyRepository,
        ILocalizer localizer,
        ILogger<BuySkipExpiryCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.itemRepository = itemRepository;
        this.historyRepository = historyRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "buy:skip_expiry";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var userId = callbackQuery.From.Id;
        var state = this.dialogService.GetState(chatId, userId);

        if (state is null || state.Name is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Диалог сброшен, попробуйте снова",
                cancellationToken: cancellationToken);
            return;
        }

        // Answer the callback
        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        // Save item without expiry date
        var item = await this.itemRepository.AddAsync(
            groupId: state.GroupId,
            name: state.Name,
            quantity: state.Quantity,
            addedByName: state.AddedByName,
            expDate: null);

        this.dialogService.ClearState(chatId, userId);

        var confirmText = item.Quantity is not null
            ? this.localizer.Get(chatId, "buy.item-added-quantity")
                .Replace("{name}", state.AddedByName).Replace("{item}", item.Name).Replace("{quantity}", item.Quantity)
            : this.localizer.Get(chatId, "buy.item-added")
                .Replace("{name}", state.AddedByName).Replace("{item}", item.Name);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: confirmText,
            cancellationToken: cancellationToken);

        try
        {
            var payload = new ItemPayload(state.Name, state.Quantity);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            await this.historyRepository.RecordAsync(
                chatId: chatId,
                userId: userId,
                userName: state.AddedByName,
                actionType: BotActionType.ItemAdded,
                payloadJson: payloadJson,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ItemAdded");
        }
    }
}
