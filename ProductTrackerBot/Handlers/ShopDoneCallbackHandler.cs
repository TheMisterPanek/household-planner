// <copyright file="ShopDoneCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
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
    private readonly PendingDialogService<TagCaptureDialogState> tagDialogService;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly ILocalizer localizer;
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
    /// <param name="tagDialogService">The tag-capture dialog state service, cleared when a price-capture prompt starts so a stale tag dialog cannot steal the reply.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public ShopDoneCallbackHandler(
        ITelegramBotClient botClient,
        ShoppingItemRepository itemRepository,
        ShoppingListService listService,
        GroupRepository groupRepository,
        IHistoryRepository historyRepository,
        PendingDialogService<PriceCaptureDialogState> priceDialogService,
        PendingDialogService<TagCaptureDialogState> tagDialogService,
        PurchaseHistoryRepository purchaseRepository,
        ILocalizer localizer,
        ILogger<ShopDoneCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.itemRepository = itemRepository;
        this.listService = listService;
        this.groupRepository = groupRepository;
        this.historyRepository = historyRepository;
        this.priceDialogService = priceDialogService;
        this.tagDialogService = tagDialogService;
        this.purchaseRepository = purchaseRepository;
        this.localizer = localizer;
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

        var confirmText = this.localizer.Get(callbackQuery.Message.Chat.Id, "shop.done-confirmation")
            .Replace("{name}", displayName ?? "Unknown")
            .Replace("{item}", buttonText);

        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: confirmText,
            cancellationToken: cancellationToken);

        try
        {
            var payload = new ItemPayload(buttonText, null);
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            var revertPayload = new ItemBoughtRevert(item.Id, item.Name, item.Quantity, item.GroupId);
            var revertPayloadJson = System.Text.Json.JsonSerializer.Serialize(revertPayload, BotActionPayloadContext.Default.ItemBoughtRevert);
            await this.historyRepository.RecordAsync(
                chatId: callbackQuery.Message.Chat.Id,
                userId: callbackQuery.From.Id,
                userName: displayName ?? "Unknown",
                actionType: BotActionType.ItemBought,
                payloadJson: payloadJson,
                revertPayloadJson: revertPayloadJson,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ItemBought");
        }

        // Fetch top shops for suggestions
        var group = await this.groupRepository.GetOrCreateAsync(callbackQuery.Message.Chat.Id);
        var topShops = await this.purchaseRepository.GetTopShopsAsync(group.Id, callbackQuery.From.Id, 5);

        // Start price-capture dialog
        var state = new PriceCaptureDialogState
        {
            Step = 1,
            GroupId = group.Id,
            ItemName = item.Name,
            Quantity = item.Quantity,
            BoughtByName = displayName ?? "Unknown",
            TopShops = topShops.Count > 0 ? new List<string>(topShops) : null,
            Tags = item.Tags,
        };
        this.tagDialogService.ClearState(callbackQuery.Message.Chat.Id, callbackQuery.From.Id);
        this.priceDialogService.SetState(callbackQuery.Message.Chat.Id, callbackQuery.From.Id, state);

        // Build keyboard with shop suggestion buttons and Skip button
        var keyboard = this.BuildShopKeyboard(callbackQuery.Message.Chat.Id, topShops);

        var whereBoughtText = this.localizer.Get(callbackQuery.Message.Chat.Id, "shop.where-bought")
            .Replace("{item}", item.Name);

        await this.botClient.SendMessage(
            chatId: callbackQuery.Message.Chat.Id,
            text: whereBoughtText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private InlineKeyboardMarkup BuildShopKeyboard(long chatId, IReadOnlyList<string> topShops)
    {
        var rows = new List<InlineKeyboardButton[]>();

        for (int i = 0; i < topShops.Count; i++)
        {
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(topShops[i], $"price:shop:{i}") });
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "shop.skip"), "price:skip_store"),
            InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "shop.undo-button"), "undo:inline"),
        });

        return new InlineKeyboardMarkup(rows);
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
