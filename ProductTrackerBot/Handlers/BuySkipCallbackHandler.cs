// <copyright file="BuySkipCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "Пропустить" callback during /buy quantity step — saves item without quantity.
/// </summary>
public class BuySkipCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly ShoppingItemRepository itemRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuySkipCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The dialog state service.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    public BuySkipCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<BuyDialogState> dialogService,
        ShoppingItemRepository itemRepository)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.itemRepository = itemRepository;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "buy:skip_quantity";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var userId = callbackQuery.From.Id;
        var state = this.dialogService.GetState(chatId, userId);

        if (state is null || state.Name is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Диалог сброшен, попробуйте снова",
                cancellationToken: cancellationToken);
            return;
        }

        // Save item without quantity
        var item = await this.itemRepository.AddAsync(
            groupId: state.GroupId,
            name: state.Name,
            quantity: null,
            addedByName: state.AddedByName);

        this.dialogService.ClearState(chatId, userId);

        // Answer the callback and remove the keyboard
        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        await this.botClient.EditMessageText(
            chatId: chatId,
            messageId: callbackQuery.Message.MessageId,
            text: $"Сколько?\n\n{state.AddedByName} добавил(а) {state.Name}",
            cancellationToken: cancellationToken);

        var confirmText = $"{state.AddedByName} добавил(а) {state.Name}";
        await this.botClient.SendMessage(
            chatId: chatId,
            text: confirmText,
            cancellationToken: cancellationToken);
    }
}
