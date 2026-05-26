// <copyright file="ExpirySuggestCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the <c>expiry:suggest:{days}</c> callback — advances the active expiry dialog
/// to completion using the tapped day count.
/// </summary>
public class ExpirySuggestCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<BoughtDialogState> boughtDialogService;
    private readonly PendingDialogService<PriceCaptureDialogState> priceDialogService;
    private readonly BoughtStepHandler boughtStepHandler;
    private readonly PriceCaptureStepHandler priceCaptureStepHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpirySuggestCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="boughtDialogService">The bought dialog state service.</param>
    /// <param name="priceDialogService">The price-capture dialog state service.</param>
    /// <param name="boughtStepHandler">The bought step handler (for FinishDialogAsync).</param>
    /// <param name="priceCaptureStepHandler">The price capture step handler (for FinishDialogAsync).</param>
    public ExpirySuggestCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<BoughtDialogState> boughtDialogService,
        PendingDialogService<PriceCaptureDialogState> priceDialogService,
        BoughtStepHandler boughtStepHandler,
        PriceCaptureStepHandler priceCaptureStepHandler)
    {
        this.botClient = botClient;
        this.boughtDialogService = boughtDialogService;
        this.priceDialogService = priceDialogService;
        this.boughtStepHandler = boughtStepHandler;
        this.priceCaptureStepHandler = priceCaptureStepHandler;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "expiry:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        // Expected format: expiry:suggest:{days}
        var parts = callbackQuery.Data.Split(':');
        if (parts.Length < 3 || parts[1] != "suggest" || !int.TryParse(parts[2], out var days) || days <= 0)
        {
            await this.botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var userId = callbackQuery.From.Id;
        var expDate = DateOnly.FromDateTime(DateTime.Now.AddDays(days));

        var boughtState = this.boughtDialogService.GetState(chatId, userId);
        if (boughtState is { Step: 2 })
        {
            await this.boughtStepHandler.FinishDialogAsync(chatId, userId, boughtState, expDate, cancellationToken);
            await this.botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        var priceState = this.priceDialogService.GetState(chatId, userId);
        if (priceState is { Step: 3 })
        {
            await this.priceCaptureStepHandler.FinishDialogAsync(chatId, userId, priceState, expDate, cancellationToken);
            await this.botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        await this.botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }
}
