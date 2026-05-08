// <copyright file="ShopDoneCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the "✓" buy button — marks an item as bought, deletes it, and optionally captures price info.
/// </summary>
public class ShopDoneCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ShoppingListService listService;
    private readonly GroupRepository groupRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly PendingDialogService<PriceCaptureDialogState> priceDialogService;
    private readonly ILogger<ShopDoneCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShopDoneCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="listService">The shopping list service.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="priceDialogService">The price-capture dialog state service.</param>
    /// <param name="logger">The logger.</param>
    public ShopDoneCallbackHandler(
        ITelegramBotClient botClient,
        ShoppingItemRepository itemRepository,
        ShoppingListService listService,
        GroupRepository groupRepository,
        IHistoryRepository historyRepository,
        PendingDialogService<PriceCaptureDialogState> priceDialogService,
        ILogger<ShopDoneCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.itemRepository = itemRepository;
        this.listService = listService;
        this.groupRepository = groupRepository;
        this.historyRepository = historyRepository;
        this.priceDialogService = priceDialogService;
        this.logger = logger;
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

        // Read item details before deleting
        var item = await this.itemRepository.GetByIdAsync(itemId);
        if (item is null)
        {
            return;
        }

        // Delete the item
        await this.itemRepository.DeleteAsync(itemId);

        // Rebuild and update the list message
        await this.UpdateListMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);

        var displayName = callbackQuery.From.FirstName;

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        // Send confirmation
        var buttonText = item.Quantity is not null
            ? $"{item.Name} {item.Quantity}"
            : item.Name;

        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: $"{displayName}: {buttonText} — отмечено ✓",
            cancellationToken: cancellationToken);

        try
        {
            var payload = new ItemPayload(buttonText, null);
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            await this.historyRepository.RecordAsync(
                chatId: callbackQuery.Message.Chat.Id,
                userId: callbackQuery.From.Id,
                userName: displayName ?? "Неизвестный",
                actionType: BotActionType.ItemBought,
                payloadJson: payloadJson,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ItemBought");
        }

        // Start price-capture dialog
        this.priceDialogService.SetState(
            callbackQuery.Message.Chat.Id,
            callbackQuery.From.Id,
            new PriceCaptureDialogState
            {
                Step = 1,
                ItemName = item.Name,
                Quantity = item.Quantity,
                BoughtByName = displayName ?? "Неизвестный",
            });

        var skipStoreButton = InlineKeyboardButton.WithCallbackData("Skip", "price:skip_store");

        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: $"📍 Where did you buy {item.Name}?",
            replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipStoreButton } }),
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
