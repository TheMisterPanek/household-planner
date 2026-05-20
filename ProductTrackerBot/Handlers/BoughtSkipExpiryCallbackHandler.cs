// <copyright file="BoughtSkipExpiryCallbackHandler.cs" company="PlaceholderCompany">
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
/// Handles the "bought:skip_expiry" callback — saves the purchase with no expiry date.
/// </summary>
public class BoughtSkipExpiryCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<BoughtDialogState> dialogService;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<BoughtSkipExpiryCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoughtSkipExpiryCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The bought dialog state service.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="logger">The logger.</param>
    public BoughtSkipExpiryCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<BoughtDialogState> dialogService,
        PurchaseHistoryRepository purchaseRepository,
        IHistoryRepository historyRepository,
        ILocalizer localizer,
        ILogger<BoughtSkipExpiryCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.purchaseRepository = purchaseRepository;
        this.historyRepository = historyRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "bought";

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

        if (state is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Dialog expired, try again",
                cancellationToken: cancellationToken);
            return;
        }

        var record = new PurchaseRecord
        {
            GroupId = state.GroupId,
            UserId = userId,
            ItemName = state.ItemName,
            Quantity = state.Quantity,
            StoreName = null,
            Price = null,
            PurchasedAt = DateTime.UtcNow,
            BoughtByName = state.BoughtByName,
            ExpDate = null,
        };

        await this.purchaseRepository.AddAsync(record);

        try
        {
            var payload = new ItemPayload(record.ItemName, record.Quantity);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            await this.historyRepository.RecordAsync(
                chatId: chatId,
                userId: userId,
                userName: state.BoughtByName,
                actionType: BotActionType.ItemBought,
                payloadJson: payloadJson,
                revertPayloadJson: null,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for /bought skip-expiry item {ItemName}", record.ItemName);
        }

        this.dialogService.ClearState(chatId, userId);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        var confirmText = this.localizer.Get(chatId, "bought.done")
            .Replace("{item}", record.ItemName);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: confirmText,
            cancellationToken: cancellationToken);
    }
}
