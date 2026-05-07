// <copyright file="ListCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
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

        var sent = await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: messageText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        await this.groupRepository.UpdateListMessageIdAsync(group.Id, sent.MessageId);
    }
}
