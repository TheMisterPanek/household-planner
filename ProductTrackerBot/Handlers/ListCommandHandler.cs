// <copyright file="ListCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

/// <summary>
/// Handles the /list command — posts or edits the persistent shopping list message.
/// </summary>
public class ListCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ShoppingListService listService;
    private readonly GroupRepository groupRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="listService">The shopping list service.</param>
    /// <param name="groupRepository">The group repository.</param>
    public ListCommandHandler(
        ITelegramBotClient botClient,
        ShoppingListService listService,
        GroupRepository groupRepository)
    {
        this.botClient = botClient;
        this.listService = listService;
        this.groupRepository = groupRepository;
    }

    /// <inheritdoc/>
    public string Command => "/list";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var (messageText, keyboard, group) = await this.listService.BuildListAsync(message.Chat.Id);

        if (group.ListMessageId.HasValue)
        {
            // Try to edit the existing list message
            try
            {
                await this.botClient.EditMessageText(
                    chatId: message.Chat.Id,
                    messageId: group.ListMessageId.Value,
                    text: messageText,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 400)
            {
                // Edit limit exceeded (48h) or message no longer exists — repost
                group.ListMessageId = null;
            }
        }

        // Post a new list message
        var sent = await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: messageText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        await this.groupRepository.UpdateListMessageIdAsync(group.Id, sent.MessageId);
    }
}
