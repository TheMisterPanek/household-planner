// <copyright file="ItemEditCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the ✏️ button on a list item — opens a one-step edit dialog.
/// </summary>
public class ItemEditCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ShoppingItemRepository itemRepository;
    private readonly PendingDialogService<EditItemDialogState> dialogService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemEditCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="dialogService">The edit item dialog state service.</param>
    /// <param name="localizer">The localizer.</param>
    public ItemEditCallbackHandler(
        ITelegramBotClient botClient,
        ShoppingItemRepository itemRepository,
        PendingDialogService<EditItemDialogState> dialogService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.itemRepository = itemRepository;
        this.dialogService = dialogService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "item:edit:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var itemIdStr = callbackQuery.Data["item:edit:".Length..];
        if (!int.TryParse(itemIdStr, out var itemId))
        {
            return;
        }

        var item = await this.itemRepository.GetByIdAsync(itemId);
        if (item is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Item not found.",
                cancellationToken: cancellationToken);
            return;
        }

        this.dialogService.SetState(callbackQuery.Message.Chat.Id, callbackQuery.From.Id, new EditItemDialogState
        {
            ItemId = item.Id,
            GroupId = item.GroupId,
            OriginalName = item.Name,
            OriginalQuantity = item.Quantity,
        });

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        var itemLabel = item.Quantity is not null
            ? $"{item.Name} ({item.Quantity})"
            : item.Name;

        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: this.localizer.Get(callbackQuery.Message.Chat.Id, "item.edit-prompt")
                .Replace("{item}", itemLabel),
            cancellationToken: cancellationToken);
    }
}
