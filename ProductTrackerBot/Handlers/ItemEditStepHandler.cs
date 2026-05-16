// <copyright file="ItemEditStepHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the user's text reply during the item-edit dialog — parses input and sends a review message.
/// </summary>
public class ItemEditStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<EditItemDialogState> dialogService;
    private readonly PendingEditService pendingEditService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemEditStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The edit item dialog state service.</param>
    /// <param name="pendingEditService">The pending edit session service.</param>
    /// <param name="localizer">The localizer.</param>
    public ItemEditStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<EditItemDialogState> dialogService,
        PendingEditService pendingEditService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.pendingEditService = pendingEditService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public bool CanHandle(long chatId, long userId)
    {
        return this.dialogService.GetState(chatId, userId) is not null;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null || message.Text is null)
        {
            return;
        }

        var state = this.dialogService.GetState(message.Chat.Id, message.From.Id);
        if (state is null)
        {
            return;
        }

        var (name, quantity) = BuyInputParser.Parse(message.Text.Trim());
        this.dialogService.ClearState(message.Chat.Id, message.From.Id);

        var token = this.pendingEditService.Store(new PendingEditItem(
            ChatId: message.Chat.Id,
            ItemId: state.ItemId,
            GroupId: state.GroupId,
            Name: name,
            Quantity: quantity));

        var reviewText = quantity is not null
            ? this.localizer.Get(message.Chat.Id, "item.review-save-qty")
                .Replace("{item}", name).Replace("{quantity}", quantity)
            : this.localizer.Get(message.Chat.Id, "item.review-save")
                .Replace("{item}", name);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(this.localizer.Get(message.Chat.Id, "item.btn-save"), $"item:save:{token}"),
                InlineKeyboardButton.WithCallbackData(this.localizer.Get(message.Chat.Id, "item.btn-cancel"), $"item:cancel-edit:{token}"),
            },
        });

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: reviewText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
