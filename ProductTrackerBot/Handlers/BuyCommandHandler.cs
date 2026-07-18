// <copyright file="BuyCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /buy command — shows a review step (/buy name qty) or initiates a 2-step dialog.
/// Supports comma-separated bulk input: /buy item1, item2 qty, item3
/// </summary>
public class BuyCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly PendingAddService pendingAddService;
    private readonly ILocalizer localizer;
    private readonly ShoppingListService shoppingListService;
    private readonly IHistoryRepository historyRepository;
    private readonly TagCaptureService tagCaptureService;
    private readonly BuyAddService buyAddService;
    private readonly ILogger<BuyCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="dialogService">The dialog state service.</param>
    /// <param name="pendingAddService">The pending add session service.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="shoppingListService">The shopping list service for bulk adds.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="tagCaptureService">The tag-capture follow-up service.</param>
    /// <param name="buyAddService">The shared persist-and-confirm service.</param>
    /// <param name="logger">The logger.</param>
    public BuyCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        PendingDialogService<BuyDialogState> dialogService,
        PendingAddService pendingAddService,
        ILocalizer localizer,
        ShoppingListService shoppingListService,
        IHistoryRepository historyRepository,
        TagCaptureService tagCaptureService,
        BuyAddService buyAddService,
        ILogger<BuyCommandHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.dialogService = dialogService;
        this.pendingAddService = pendingAddService;
        this.localizer = localizer;
        this.shoppingListService = shoppingListService;
        this.historyRepository = historyRepository;
        this.tagCaptureService = tagCaptureService;
        this.buyAddService = buyAddService;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Command => "/buy";

    /// <inheritdoc/>
    public string? Description => "Start shopping session";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var group = await this.groupRepository.GetOrCreateAsync(message.Chat.Id);
        var displayName = message.From?.FirstName ?? message.From?.Username ?? "Unknown";

        var inlineArgs = ExtractInlineArgs(message.Text);
        if (inlineArgs is not null)
        {
            if (inlineArgs.Contains(','))
            {
                await this.HandleBulkAddAsync(message, group, displayName, inlineArgs, cancellationToken);
                return;
            }

            var (name, quantity) = BuyInputParser.Parse(inlineArgs);

            if (quantity is null)
            {
                await this.buyAddService.AddAndConfirmAsync(
                    chatId: message.Chat.Id,
                    userId: message.From!.Id,
                    groupId: group.Id,
                    name: name,
                    quantity: quantity,
                    addedByName: displayName,
                    cancellationToken: cancellationToken);
                return;
            }

            var token = this.pendingAddService.Store(new PendingAddItem(
                ChatId: message.Chat.Id,
                GroupId: group.Id,
                Name: name,
                Quantity: quantity,
                AddedByName: displayName));

            var reviewText = quantity is not null
                ? this.localizer.Get(message.Chat.Id, "buy.review-add-qty")
                    .Replace("{item}", name).Replace("{quantity}", quantity)
                : this.localizer.Get(message.Chat.Id, "buy.review-add")
                    .Replace("{item}", name);

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(message.Chat.Id, "buy.btn-confirm"), $"buy:confirm:{token}"),
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(message.Chat.Id, "buy.btn-edit"), $"buy:edit:{token}"),
                    InlineKeyboardButton.WithCallbackData(this.localizer.Get(message.Chat.Id, "buy.btn-cancel"), $"buy:cancel:{token}"),
                },
            });

            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: reviewText,
                replyMarkup: keyboard,
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);

            return;
        }

        this.dialogService.ClearState(message.Chat.Id, message.From!.Id);
        this.dialogService.SetState(message.Chat.Id, message.From.Id, new BuyDialogState
        {
            Step = 1,
            GroupId = group.Id,
            AddedByName = displayName,
        });

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: this.localizer.Get(message.Chat.Id, "buy.what-to-buy"),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleBulkAddAsync(
        Message message,
        Group group,
        string displayName,
        string rawCsv,
        CancellationToken cancellationToken)
    {
        var addedItems = await this.shoppingListService.AddItemsAsync(rawCsv, group.Id, displayName);

        if (addedItems.Count == 0)
        {
            return;
        }

        foreach (var item in addedItems)
        {
            try
            {
                var payload = new ItemPayload(item.Name, item.Quantity);
                var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
                await this.historyRepository.RecordAsync(
                    chatId: message.Chat.Id,
                    userId: message.From!.Id,
                    userName: displayName,
                    actionType: BotActionType.ItemAdded,
                    payloadJson: payloadJson,
                    revertPayloadJson: null,
                    ct: cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to record history for bulk ItemAdded");
            }
        }

        var itemLabels = addedItems.Select(i =>
            i.Quantity is not null ? $"{i.Name} {i.Quantity}" : i.Name);

        var confirmText = this.localizer.Get(message.Chat.Id, "shop.bulk-added")
            .Replace("{User}", displayName)
            .Replace("{Count}", addedItems.Count.ToString())
            .Replace("{Items}", string.Join(", ", itemLabels));

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: confirmText,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);

        var bulkLabel = this.localizer.Get(message.Chat.Id, "category.bulk-label")
            .Replace("{count}", addedItems.Count.ToString());

        await this.tagCaptureService.StartTagCaptureAsync(
            message.Chat.Id,
            message.From!.Id,
            group.Id,
            addedItems.Select(i => i.Id).ToList(),
            bulkLabel,
            preselectedTags: null,
            cancellationToken);
    }

    private static string? ExtractInlineArgs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var spaceIdx = text.IndexOf(' ');
        if (spaceIdx < 0)
        {
            return null;
        }

        var args = text[(spaceIdx + 1)..].Trim();
        return string.IsNullOrEmpty(args) ? null : args;
    }
}
