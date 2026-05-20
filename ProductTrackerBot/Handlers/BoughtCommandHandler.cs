// <copyright file="BoughtCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /bought command — registers a purchased item with an optional expiry date.
/// </summary>
public class BoughtCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly PendingDialogService<BoughtDialogState> dialogService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoughtCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="dialogService">The bought dialog state service.</param>
    /// <param name="localizer">The localizer.</param>
    public BoughtCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        PendingDialogService<BoughtDialogState> dialogService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.dialogService = dialogService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string Command => "/bought";

    /// <inheritdoc/>
    public string? Description => "Register a purchased item with expiry";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var displayName = message.From?.FirstName ?? message.From?.Username ?? "Unknown";

        if (message.Chat.Type == ChatType.Private)
        {
            await this.botClient.SendMessage(
                chatId: chatId,
                text: this.localizer.Get(chatId, "common.group-only"),
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
            return;
        }

        var group = await this.groupRepository.GetOrCreateAsync(chatId);
        var inlineArgs = ExtractInlineArgs(message.Text);

        if (inlineArgs is not null)
        {
            var (itemName, quantity) = ParseItemInput(inlineArgs);
            this.dialogService.SetState(chatId, userId, new BoughtDialogState
            {
                Step = 2,
                ItemName = itemName,
                Quantity = quantity,
                GroupId = group.Id,
                BoughtByName = displayName,
            });

            var expiryPrompt = this.localizer.Get(chatId, "bought.expiry-prompt")
                .Replace("{item}", itemName);
            var skipButton = InlineKeyboardButton.WithCallbackData(
                this.localizer.Get(chatId, "bought.skip-expiry"),
                "bought:skip_expiry");

            await this.botClient.SendMessage(
                chatId: chatId,
                text: expiryPrompt,
                replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipButton } }),
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
            return;
        }

        this.dialogService.SetState(chatId, userId, new BoughtDialogState
        {
            Step = 1,
            GroupId = group.Id,
            BoughtByName = displayName,
        });

        await this.botClient.SendMessage(
            chatId: chatId,
            text: this.localizer.Get(chatId, "bought.what-did-you-buy"),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
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

    private static (string itemName, string? quantity) ParseItemInput(string input)
    {
        var spaceIdx = input.IndexOf(' ');
        if (spaceIdx < 0)
        {
            return (input.Trim(), null);
        }

        var name = input[..spaceIdx].Trim();
        var qty = input[(spaceIdx + 1)..].Trim();
        return (name, string.IsNullOrEmpty(qty) ? null : qty);
    }
}
