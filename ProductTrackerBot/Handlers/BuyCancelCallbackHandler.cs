// <copyright file="BuyCancelCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "✕ Cancel" button from a buy review message.
/// </summary>
public class BuyCancelCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingAddService pendingAddService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyCancelCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="pendingAddService">The pending add session service.</param>
    /// <param name="localizer">The localizer.</param>
    public BuyCancelCallbackHandler(
        ITelegramBotClient botClient,
        PendingAddService pendingAddService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.pendingAddService = pendingAddService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "buy:cancel:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var token = callbackQuery.Data["buy:cancel:".Length..];
        this.pendingAddService.Clear(token);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: this.localizer.Get(callbackQuery.Message.Chat.Id, "buy.cancelled"),
            cancellationToken: cancellationToken);
    }
}
