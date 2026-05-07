// <copyright file="BuyCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /buy command — initiates a 2-step dialog to add a shopping item.
/// </summary>
public class BuyCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly PendingDialogService<BuyDialogState> dialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="dialogService">The dialog state service.</param>
    public BuyCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        PendingDialogService<BuyDialogState> dialogService)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.dialogService = dialogService;
    }

    /// <inheritdoc/>
    public string Command => "/buy";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type != ChatType.Group && message.Chat.Type != ChatType.Supergroup)
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Эта команда работает только в групповом чате.",
                cancellationToken: cancellationToken);
            return;
        }

        var group = await this.groupRepository.GetOrCreateAsync(message.Chat.Id);
        var displayName = message.From?.FirstName ?? message.From?.Username ?? "Неизвестный";

        // Clear any existing dialog state and start new
        this.dialogService.ClearState(message.Chat.Id, message.From!.Id);
        this.dialogService.SetState(message.Chat.Id, message.From.Id, new BuyDialogState
        {
            Step = 1,
            GroupId = group.Id,
            AddedByName = displayName,
        });

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Что купить?",
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }
}
