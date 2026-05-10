// <copyright file="PriceSkipCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the "Skip" callbacks during the price-capture dialog.
/// Callback data: "price:skip_store" (step 1), "price:skip_price" (step 2), or "price:skip_expiry" (step 3).
/// </summary>
public class PriceSkipCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<PriceCaptureDialogState> dialogService;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly GroupRepository groupRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<PriceSkipCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceSkipCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The price-capture dialog state service.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public PriceSkipCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<PriceCaptureDialogState> dialogService,
        PurchaseHistoryRepository purchaseRepository,
        GroupRepository groupRepository,
        ILocalizer localizer,
        ILogger<PriceSkipCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.purchaseRepository = purchaseRepository;
        this.groupRepository = groupRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "price:skip_";

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

        if (state.Step == 1)
        {
            // Skip store name → advance to step 2
            state.Step = 2;
            this.dialogService.SetState(chatId, userId, state);

            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                cancellationToken: cancellationToken);

            await this.botClient.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: $"📍 Where did you buy {state.ItemName}?\n\nSkipped",
                cancellationToken: cancellationToken);

            var skipPriceButton = InlineKeyboardButton.WithCallbackData(
                this.localizer.Get(chatId, "shop.skip"),
                "price:skip_price");

            await this.botClient.SendMessage(
                chatId: chatId,
                text: $"💰 Price for {state.ItemName}?",
                replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipPriceButton } }),
                cancellationToken: cancellationToken);
        }
        else if (state.Step == 2)
        {
            // Skip price → advance to step 3 for expiry
            state.Price = null;
            state.Step = 3;
            this.dialogService.SetState(chatId, userId, state);

            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                cancellationToken: cancellationToken);

            await this.botClient.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: $"💰 Price for {state.ItemName}?\n\nSkipped",
                cancellationToken: cancellationToken);

            var skipExpiryButton = InlineKeyboardButton.WithCallbackData(
                this.localizer.Get(chatId, "shop.skip"),
                "price:skip_expiry");

            var expiryPrompt = this.localizer.Get(chatId, "shop.expiry-prompt")
                .Replace("{item}", state.ItemName);

            await this.botClient.SendMessage(
                chatId: chatId,
                text: expiryPrompt,
                replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipExpiryButton } }),
                cancellationToken: cancellationToken);
        }
        else if (state.Step == 3)
        {
            // Skip expiry → save record with null exp_date
            var group = await this.groupRepository.GetOrCreateAsync(chatId);

            var record = new PurchaseRecord
            {
                GroupId = group.Id,
                UserId = userId,
                ItemName = state.ItemName,
                Quantity = state.Quantity,
                StoreName = state.StoreName,
                Price = state.Price,
                PurchasedAt = DateTime.UtcNow,
                BoughtByName = state.BoughtByName,
                ExpDate = null,
            };

            await this.purchaseRepository.AddAsync(record);

            this.dialogService.ClearState(chatId, userId);

            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                cancellationToken: cancellationToken);

            await this.botClient.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: this.localizer.Get(chatId, "shop.expiry-prompt")
                    .Replace("{item}", state.ItemName) + "\n\nSkipped",
                cancellationToken: cancellationToken);

            var details = string.Empty;
            if (record.StoreName is not null)
            {
                details += $" at {record.StoreName}";
            }

            if (record.Price.HasValue)
            {
                details += $" for {record.Price.Value:F2}";
            }

            await this.botClient.SendMessage(
                chatId: chatId,
                text: $"✓ {record.ItemName} recorded{details}",
                cancellationToken: cancellationToken);
        }
    }
}
