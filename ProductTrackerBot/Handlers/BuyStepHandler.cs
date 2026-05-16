// <copyright file="BuyStepHandler.cs" company="PlaceholderCompany">
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
/// Processes dialog steps for the /buy command — step 1 (item name), step 2 (quantity).
/// Step 2 now routes to a review step instead of saving directly.
/// </summary>
public class BuyStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly PendingAddService pendingAddService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The dialog state service.</param>
    /// <param name="pendingAddService">The pending add session service.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public BuyStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<BuyDialogState> dialogService,
        PendingAddService pendingAddService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.pendingAddService = pendingAddService;
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

        switch (state.Step)
        {
            case 1:
                await this.HandleStep1Async(message, state, cancellationToken);
                break;
            case 2:
                await this.HandleStep2Async(message, state, cancellationToken);
                break;
        }
    }

    private async Task HandleStep1Async(Message message, BuyDialogState state, CancellationToken cancellationToken)
    {
        if (state.IsOneLineMode)
        {
            var (name, quantity) = BuyInputParser.Parse(message.Text!.Trim());
            this.dialogService.ClearState(message.Chat.Id, message.From!.Id);

            var token = this.pendingAddService.Store(new PendingAddItem(
                ChatId: message.Chat.Id,
                GroupId: state.GroupId,
                Name: name,
                Quantity: quantity,
                AddedByName: state.AddedByName));

            await this.SendReviewMessageAsync(message.Chat.Id, name, quantity, token, twoButtonMode: true, cancellationToken);
            return;
        }

        state.Name = message.Text!.Trim();
        state.Step = 2;
        this.dialogService.SetState(message.Chat.Id, message.From!.Id, state);

        var skipButton = InlineKeyboardButton.WithCallbackData(
            this.localizer.Get(message.Chat.Id, "buy.skip"),
            "buy:skip_quantity");

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: this.localizer.Get(message.Chat.Id, "buy.how-much"),
            replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipButton } }),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep2Async(Message message, BuyDialogState state, CancellationToken cancellationToken)
    {
        var quantity = message.Text!.Trim();
        this.dialogService.ClearState(message.Chat.Id, message.From!.Id);

        var token = this.pendingAddService.Store(new PendingAddItem(
            ChatId: message.Chat.Id,
            GroupId: state.GroupId,
            Name: state.Name!,
            Quantity: quantity,
            AddedByName: state.AddedByName));

        await this.SendReviewMessageAsync(message.Chat.Id, state.Name!, quantity, token, twoButtonMode: false, cancellationToken);
    }

    private async Task SendReviewMessageAsync(
        long chatId,
        string name,
        string? quantity,
        string token,
        bool twoButtonMode,
        CancellationToken cancellationToken)
    {
        var reviewText = quantity is not null
            ? this.localizer.Get(chatId, "buy.review-add-qty")
                .Replace("{item}", name).Replace("{quantity}", quantity)
            : this.localizer.Get(chatId, "buy.review-add")
                .Replace("{item}", name);

        InlineKeyboardMarkup keyboard;
        if (twoButtonMode)
        {
            keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "buy.btn-confirm"), $"buy:confirm:{token}"),
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "buy.btn-cancel"), $"buy:cancel:{token}"),
                },
            });
        }
        else
        {
            keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "buy.btn-confirm"), $"buy:confirm:{token}"),
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "buy.btn-edit"), $"buy:edit:{token}"),
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "buy.btn-cancel"), $"buy:cancel:{token}"),
                },
            });
        }

        await this.botClient.SendMessage(
            chatId: chatId,
            text: reviewText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
