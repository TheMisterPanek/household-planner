// <copyright file="BuyCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /buy command — shows a review step (/buy name qty) or initiates a 2-step dialog.
/// </summary>
public class BuyCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly PendingAddService pendingAddService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="dialogService">The dialog state service.</param>
    /// <param name="pendingAddService">The pending add session service.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public BuyCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        PendingDialogService<BuyDialogState> dialogService,
        PendingAddService pendingAddService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.dialogService = dialogService;
        this.pendingAddService = pendingAddService;
        this.localizer = localizer;
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
            var (name, quantity) = BuyInputParser.Parse(inlineArgs);
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
