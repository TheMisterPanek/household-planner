// <copyright file="BuyAddService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;

/// <summary>
/// Persists a shopping item and sends the confirmation, history record, and category-capture
/// follow-up shared by the /buy review-confirm flow and the no-quantity direct-add flow.
/// </summary>
public class BuyAddService
{
    private readonly ITelegramBotClient botClient;
    private readonly ShoppingItemRepository itemRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly CategoryCaptureService categoryCaptureService;
    private readonly ILocalizer localizer;
    private readonly ILogger<BuyAddService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyAddService"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="categoryCaptureService">The category-capture follow-up service.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="logger">The logger.</param>
    public BuyAddService(
        ITelegramBotClient botClient,
        ShoppingItemRepository itemRepository,
        IHistoryRepository historyRepository,
        CategoryCaptureService categoryCaptureService,
        ILocalizer localizer,
        ILogger<BuyAddService> logger)
    {
        this.botClient = botClient;
        this.itemRepository = itemRepository;
        this.historyRepository = historyRepository;
        this.categoryCaptureService = categoryCaptureService;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <summary>
    /// Persists the item, sends the "added" confirmation, records history, and starts the
    /// category-capture follow-up — the shared tail end of both the review-confirm path and
    /// the no-quantity direct-add path.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="userId">The Telegram user ID performing the add.</param>
    /// <param name="groupId">The group ID.</param>
    /// <param name="name">The item name.</param>
    /// <param name="quantity">The item quantity, or <see langword="null"/> if none was detected.</param>
    /// <param name="addedByName">The display name of the user adding the item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The persisted <see cref="ShoppingItem"/>.</returns>
    public virtual async Task<ShoppingItem> AddAndConfirmAsync(
        long chatId,
        long userId,
        int groupId,
        string name,
        string? quantity,
        string addedByName,
        CancellationToken cancellationToken)
    {
        var item = await this.itemRepository.AddAsync(
            groupId: groupId,
            name: name,
            quantity: quantity,
            addedByName: addedByName,
            expDate: null);

        var confirmText = item.Quantity is not null
            ? this.localizer.Get(chatId, "buy.item-added-quantity")
                .Replace("{name}", addedByName).Replace("{item}", item.Name).Replace("{quantity}", item.Quantity)
            : this.localizer.Get(chatId, "buy.item-added")
                .Replace("{name}", addedByName).Replace("{item}", item.Name);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: confirmText,
            cancellationToken: cancellationToken);

        try
        {
            var payload = new ItemPayload(item.Name, item.Quantity);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            await this.historyRepository.RecordAsync(
                chatId: chatId,
                userId: userId,
                userName: addedByName,
                actionType: BotActionType.ItemAdded,
                payloadJson: payloadJson,
                revertPayloadJson: null,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ItemAdded");
        }

        await this.categoryCaptureService.StartCategoryCaptureAsync(
            chatId, userId, groupId, new[] { item.Id }, item.Name, cancellationToken);

        return item;
    }
}
