// <copyright file="BuyEditCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "✏️ Edit" button from a buy review message — re-opens dialog in one-line mode.
/// </summary>
public class BuyEditCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingAddService pendingAddService;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyEditCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="pendingAddService">The pending add session service.</param>
    /// <param name="dialogService">The buy dialog state service.</param>
    /// <param name="localizer">The localizer.</param>
    public BuyEditCallbackHandler(
        ITelegramBotClient botClient,
        PendingAddService pendingAddService,
        PendingDialogService<BuyDialogState> dialogService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.pendingAddService = pendingAddService;
        this.dialogService = dialogService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "buy:edit:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var token = callbackQuery.Data["buy:edit:".Length..];
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

        this.dialogService.SetState(callbackQuery.Message.Chat.Id, callbackQuery.From.Id, new BuyDialogState
        {
            Step = 1,
            IsOneLineMode = true,
            GroupId = pending.GroupId,
            AddedByName = pending.AddedByName,
        });

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: this.localizer.Get(callbackQuery.Message.Chat.Id, "buy.edit-prompt"),
            cancellationToken: cancellationToken);
    }
}
