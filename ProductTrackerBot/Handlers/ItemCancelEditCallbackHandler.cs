// <copyright file="ItemCancelEditCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "✕ Cancel" button from an item-edit review message.
/// </summary>
public class ItemCancelEditCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingEditService pendingEditService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemCancelEditCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="pendingEditService">The pending edit session service.</param>
    /// <param name="localizer">The localizer.</param>
    public ItemCancelEditCallbackHandler(
        ITelegramBotClient botClient,
        PendingEditService pendingEditService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.pendingEditService = pendingEditService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "item:cancel-edit:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var token = callbackQuery.Data["item:cancel-edit:".Length..];
        this.pendingEditService.Clear(token);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: this.localizer.Get(callbackQuery.Message.Chat.Id, "buy.cancelled"),
            cancellationToken: cancellationToken);
    }
}
