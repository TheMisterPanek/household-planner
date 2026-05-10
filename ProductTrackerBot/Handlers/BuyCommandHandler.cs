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
/// Handles the /buy command — adds an item directly (/buy name qty) or initiates a 2-step dialog.
/// </summary>
public class BuyCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly ShoppingItemRepository itemRepository;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly IHistoryRepository historyRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<BuyCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="dialogService">The dialog state service.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public BuyCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        ShoppingItemRepository itemRepository,
        PendingDialogService<BuyDialogState> dialogService,
        IHistoryRepository historyRepository,
        ILocalizer localizer,
        ILogger<BuyCommandHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.itemRepository = itemRepository;
        this.dialogService = dialogService;
        this.historyRepository = historyRepository;
        this.localizer = localizer;
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

        var args = ParseArgs(message.Text);
        if (args.HasValue)
        {
            var item = await this.itemRepository.AddAsync(group.Id, args.Value.Name, args.Value.Quantity, displayName);
            var confirm = item.Quantity is not null
                ? this.localizer.Get(message.Chat.Id, "buy.item-added-quantity")
                    .Replace("{name}", displayName).Replace("{item}", item.Name).Replace("{quantity}", item.Quantity)
                : this.localizer.Get(message.Chat.Id, "buy.item-added")
                    .Replace("{name}", displayName).Replace("{item}", item.Name);
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: confirm,
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);

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
                    ct: cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to record history for ItemAdded");
            }

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

    private static (string Name, string? Quantity)? ParseArgs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Strip "/buy" or "/buy@botname"
        var spaceIdx = text.IndexOf(' ');
        if (spaceIdx < 0)
        {
            return null;
        }

        var args = text[(spaceIdx + 1)..].Trim();
        if (string.IsNullOrEmpty(args))
        {
            return null;
        }

        var lastSpace = args.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            return (args, null);
        }

        return (args[..lastSpace].Trim(), args[(lastSpace + 1)..].Trim());
    }
}
