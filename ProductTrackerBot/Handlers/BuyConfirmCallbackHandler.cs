// <copyright file="BuyConfirmCallbackHandler.cs" company="PlaceholderCompany">
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
/// Handles the "✓ Add" confirmation button from a buy review message.
/// </summary>
public class BuyConfirmCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingAddService pendingAddService;
    private readonly ShoppingItemRepository itemRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly CategoryCaptureService categoryCaptureService;
    private readonly ILocalizer localizer;
    private readonly ILogger<BuyConfirmCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyConfirmCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="pendingAddService">The pending add session service.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="categoryCaptureService">The category-capture follow-up service.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="logger">The logger.</param>
    public BuyConfirmCallbackHandler(
        ITelegramBotClient botClient,
        PendingAddService pendingAddService,
        ShoppingItemRepository itemRepository,
        IHistoryRepository historyRepository,
        CategoryCaptureService categoryCaptureService,
        ILocalizer localizer,
        ILogger<BuyConfirmCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.pendingAddService = pendingAddService;
        this.itemRepository = itemRepository;
        this.historyRepository = historyRepository;
        this.categoryCaptureService = categoryCaptureService;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "buy:confirm:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var token = callbackQuery.Data["buy:confirm:".Length..];
        var pending = this.pendingAddService.Get(token);

        if (pending is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Session expired.",
                cancellationToken: cancellationToken);
            return;
        }

        this.pendingAddService.Clear(token);

        var item = await this.itemRepository.AddAsync(
            groupId: pending.GroupId,
            name: pending.Name,
            quantity: pending.Quantity,
            addedByName: pending.AddedByName,
            expDate: null);

        var confirmText = item.Quantity is not null
            ? this.localizer.Get(pending.ChatId, "buy.item-added-quantity")
                .Replace("{name}", pending.AddedByName).Replace("{item}", item.Name).Replace("{quantity}", item.Quantity)
            : this.localizer.Get(pending.ChatId, "buy.item-added")
                .Replace("{name}", pending.AddedByName).Replace("{item}", item.Name);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        await this.botClient.SendMessage(
            chatId: pending.ChatId,
            text: confirmText,
            cancellationToken: cancellationToken);

        try
        {
            var payload = new ItemPayload(item.Name, item.Quantity);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            await this.historyRepository.RecordAsync(
                chatId: pending.ChatId,
                userId: callbackQuery.From.Id,
                userName: pending.AddedByName,
                actionType: BotActionType.ItemAdded,
                payloadJson: payloadJson,
                revertPayloadJson: null,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ItemAdded");
        }

        await this.categoryCaptureService.StartCategoryCaptureAsync(
            pending.ChatId, callbackQuery.From.Id, pending.GroupId, new[] { item.Id }, item.Name, cancellationToken);
    }
}
