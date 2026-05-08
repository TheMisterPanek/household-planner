// <copyright file="PriceShopSuggestionCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles shop suggestion button taps in the price-capture dialog step 1.
/// </summary>
public class PriceShopSuggestionCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<PriceCaptureDialogState> dialogService;
    private readonly ILogger<PriceShopSuggestionCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceShopSuggestionCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The price-capture dialog state service.</param>
    /// <param name="logger">The logger.</param>
    public PriceShopSuggestionCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<PriceCaptureDialogState> dialogService,
        ILogger<PriceShopSuggestionCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "price:shop:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var userId = callbackQuery.From.Id;
        var state = this.dialogService.GetState(chatId, userId);

        if (state is null || state.TopShops is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Dialog expired, please try again",
                cancellationToken: cancellationToken);
            return;
        }

        var shopIndexStr = callbackQuery.Data["price:shop:".Length..];
        if (!int.TryParse(shopIndexStr, out var shopIndex) || shopIndex < 0 || shopIndex >= state.TopShops.Count)
        {
            this.logger.LogWarning("Invalid shop index in callback: {Index}", shopIndexStr);
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Invalid shop selection",
                cancellationToken: cancellationToken);
            return;
        }

        var selectedShop = state.TopShops[shopIndex];
        state.StoreName = selectedShop;
        state.Step = 2;
        this.dialogService.SetState(chatId, userId, state);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        var skipPriceButton = InlineKeyboardButton.WithCallbackData("Skip", "price:skip_price");

        await this.botClient.SendMessage(
            chatId: chatId,
            text: $"💰 Price for {state.ItemName}?",
            replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipPriceButton } }),
            replyParameters: new ReplyParameters { MessageId = callbackQuery.Message.MessageId },
            cancellationToken: cancellationToken);
    }
}
