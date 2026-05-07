// <copyright file="BuyStepHandler.cs" company="PlaceholderCompany">
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
/// Processes dialog steps for the /buy command — step 1 (item name) and step 2 (quantity).
/// </summary>
public class BuyStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly ShoppingItemRepository itemRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The dialog state service.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    public BuyStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<BuyDialogState> dialogService,
        ShoppingItemRepository itemRepository)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.itemRepository = itemRepository;
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

        var chatId = message.Chat.Id;
        var userId = message.From.Id;

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
        state.Name = message.Text!.Trim();
        state.Step = 2;
        this.dialogService.SetState(message.Chat.Id, message.From!.Id, state);

        var skipButton = InlineKeyboardButton.WithCallbackData("Пропустить", "buy:skip_quantity");

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Сколько?",
            replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipButton } }),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep2Async(Message message, BuyDialogState state, CancellationToken cancellationToken)
    {
        var quantity = message.Text!.Trim();
        await this.FinishDialogAsync(message.Chat.Id, message.From!.Id, state, quantity, cancellationToken);
    }

    private async Task FinishDialogAsync(
        long chatId,
        long userId,
        BuyDialogState state,
        string? quantity,
        CancellationToken cancellationToken)
    {
        var item = await this.itemRepository.AddAsync(
            groupId: state.GroupId,
            name: state.Name!,
            quantity: quantity,
            addedByName: state.AddedByName);

        this.dialogService.ClearState(chatId, userId);

        var confirmText = item.Quantity is not null
            ? $"{state.AddedByName} добавил(а) {item.Name} {item.Quantity}"
            : $"{state.AddedByName} добавил(а) {item.Name}";

        await this.botClient.SendMessage(
            chatId: chatId,
            text: confirmText,
            cancellationToken: cancellationToken);
    }
}
