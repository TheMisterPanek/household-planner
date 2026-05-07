// <copyright file="ShopRemoveCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "✗ Убрать" button — removes an item from the list without buying.
/// </summary>
public class ShopRemoveCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ShoppingListService listService;
    private readonly GroupRepository groupRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShopRemoveCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="listService">The shopping list service.</param>
    /// <param name="groupRepository">The group repository.</param>
    public ShopRemoveCallbackHandler(
        ITelegramBotClient botClient,
        ShoppingItemRepository itemRepository,
        ShoppingListService listService,
        GroupRepository groupRepository)
    {
        this.botClient = botClient;
        this.itemRepository = itemRepository;
        this.listService = listService;
        this.groupRepository = groupRepository;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "shop:remove:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var itemIdStr = callbackQuery.Data["shop:remove:".Length..];
        if (!int.TryParse(itemIdStr, out var itemId))
        {
            return;
        }

        // Delete the item
        await this.itemRepository.DeleteAsync(itemId);

        // Rebuild and update the list message
        var (messageText, keyboard, group) = await this.listService.BuildListAsync(callbackQuery.Message.Chat.Id);

        try
        {
            await this.botClient.EditMessageText(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 400)
        {
            var sent = await this.botClient.SendMessage(
                chatId: callbackQuery.Message.Chat.Id,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);

            await this.groupRepository.UpdateListMessageIdAsync(group.Id, sent.MessageId);
        }

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);
    }
}
