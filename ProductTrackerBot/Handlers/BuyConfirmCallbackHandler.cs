// <copyright file="BuyConfirmCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

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
    private readonly BuyAddService buyAddService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyConfirmCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="pendingAddService">The pending add session service.</param>
    /// <param name="buyAddService">The shared persist-and-confirm service.</param>
    public BuyConfirmCallbackHandler(
        ITelegramBotClient botClient,
        PendingAddService pendingAddService,
        BuyAddService buyAddService)
    {
        this.botClient = botClient;
        this.pendingAddService = pendingAddService;
        this.buyAddService = buyAddService;
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

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        await this.buyAddService.AddAndConfirmAsync(
            chatId: pending.ChatId,
            userId: callbackQuery.From.Id,
            groupId: pending.GroupId,
            name: pending.Name,
            quantity: pending.Quantity,
            addedByName: pending.AddedByName,
            cancellationToken: cancellationToken);
    }
}
