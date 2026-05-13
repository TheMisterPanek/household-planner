// <copyright file="UndoInlineCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "↩️ Undo" inline button on the price-capture prompt — reverts the last bought action.
/// </summary>
public class UndoInlineCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly IUndoService undoService;
    private readonly PendingDialogService<PriceCaptureDialogState> priceDialogService;
    private readonly ILocalizer localizer;
    private readonly ILogger<UndoInlineCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UndoInlineCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="undoService">The undo service.</param>
    /// <param name="priceDialogService">The price-capture dialog state service.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="logger">The logger.</param>
    public UndoInlineCallbackHandler(
        ITelegramBotClient botClient,
        IUndoService undoService,
        PendingDialogService<PriceCaptureDialogState> priceDialogService,
        ILocalizer localizer,
        ILogger<UndoInlineCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.undoService = undoService;
        this.priceDialogService = priceDialogService;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "undo:inline";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var userId = callbackQuery.From.Id;

        // Discard price-capture dialog so the partial flow is cleaned up
        this.priceDialogService.ClearState(chatId, userId);

        var resultKey = await this.undoService.UndoLastAsync(chatId, userId, cancellationToken);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        // Remove the keyboard so the button can't be pressed again
        try
        {
            await this.botClient.EditMessageReplyMarkup(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                replyMarkup: null,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to remove inline keyboard after undo");
        }

        await this.botClient.SendMessage(
            chatId: chatId,
            text: this.localizer.Get(chatId, resultKey),
            cancellationToken: cancellationToken);
    }
}
