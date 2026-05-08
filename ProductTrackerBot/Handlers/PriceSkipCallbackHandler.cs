// <copyright file="PriceSkipCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the "Skip" callbacks during the price-capture dialog.
/// Callback data: "price:skip_store" (step 1) or "price:skip_price" (step 2).
/// </summary>
public class PriceSkipCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<PriceCaptureDialogState> dialogService;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly GroupRepository groupRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceSkipCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The price-capture dialog state service.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="groupRepository">The group repository.</param>
    public PriceSkipCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<PriceCaptureDialogState> dialogService,
        PurchaseHistoryRepository purchaseRepository,
        GroupRepository groupRepository)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.purchaseRepository = purchaseRepository;
        this.groupRepository = groupRepository;
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

            var skipPriceButton = InlineKeyboardButton.WithCallbackData("Skip", "price:skip_price");

            await this.botClient.SendMessage(
                chatId: chatId,
                text: $"💰 Price for {state.ItemName}?",
                replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipPriceButton } }),
                cancellationToken: cancellationToken);
        }
        else if (state.Step == 2)
        {
            // Skip price → save record with null price
            var group = await this.groupRepository.GetOrCreateAsync(chatId);

            var record = new PurchaseRecord
            {
                GroupId = group.Id,
                ItemName = state.ItemName,
                Quantity = state.Quantity,
                StoreName = state.StoreName,
                Price = null,
                PurchasedAt = DateTime.UtcNow,
                BoughtByName = state.BoughtByName,
            };

            await this.purchaseRepository.AddAsync(record);

            this.dialogService.ClearState(chatId, userId);

            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                cancellationToken: cancellationToken);

            await this.botClient.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: $"💰 Price for {state.ItemName}?\n\nSkipped",
                cancellationToken: cancellationToken);

            var details = string.Empty;
            if (record.StoreName is not null)
            {
                details += $" at {record.StoreName}";
            }

            await this.botClient.SendMessage(
                chatId: chatId,
                text: $"✓ {record.ItemName} recorded{details}",
                cancellationToken: cancellationToken);
        }
    }
}
