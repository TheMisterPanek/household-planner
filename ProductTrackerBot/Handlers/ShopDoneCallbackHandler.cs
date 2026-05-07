// <copyright file="ShopDoneCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "✓" buy button — marks an item as bought, deletes it, and updates the list.
/// </summary>
public class ShopDoneCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ShoppingListService listService;
    private readonly GroupRepository groupRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShopDoneCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="listService">The shopping list service.</param>
    /// <param name="groupRepository">The group repository.</param>
    public ShopDoneCallbackHandler(
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
    public string CallbackPrefix => "shop:done:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var itemIdStr = callbackQuery.Data["shop:done:".Length..];
        if (!int.TryParse(itemIdStr, out var itemId))
        {
            return;
        }

        // Delete the item
        await this.itemRepository.DeleteAsync(itemId);

        // Rebuild and update the list message
        await this.UpdateListMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);

        // Build confirmation text from callback button text
        var displayName = callbackQuery.From.FirstName;
        var buttonText = "неизвестно";

        // Extract item description from the button text (format: "✓ Name qty")
        if (callbackQuery.Message.ReplyMarkup is Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup markup)
        {
            foreach (var row in markup.InlineKeyboard)
            {
                foreach (var btn in row)
                {
                    if (btn.CallbackData == callbackQuery.Data && btn.Text.StartsWith("✓ "))
                    {
                        buttonText = btn.Text[2..]; // Remove "✓ "
                        break;
                    }
                }
            }
        }

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        // Send confirmation
        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: $"{displayName}: {buttonText} — отмечено ✓",
            cancellationToken: cancellationToken);
    }

    private async Task UpdateListMessageAsync(long chatId, int messageId, CancellationToken cancellationToken)
    {
        var (messageText, keyboard, group) = await this.listService.BuildListAsync(chatId);

        try
        {
            await this.botClient.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 400)
        {
            // Post a new message if edit fails
            var sent = await this.botClient.SendMessage(
                chatId: chatId,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);

            await this.groupRepository.UpdateListMessageIdAsync(group.Id, sent.MessageId);
        }
    }
}
