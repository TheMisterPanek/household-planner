// <copyright file="ListCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
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
    private readonly IHistoryRepository historyRepository;
    private readonly ILogger<ListCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="listService">The shopping list service.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="logger">The logger.</param>
    public ListCommandHandler(
        ITelegramBotClient botClient,
        ShoppingListService listService,
        GroupRepository groupRepository,
        IHistoryRepository historyRepository,
        ILogger<ListCommandHandler> logger)
    {
        this.botClient = botClient;
        this.listService = listService;
        this.groupRepository = groupRepository;
        this.historyRepository = historyRepository;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Command => "/list";

    /// <inheritdoc/>
    public string? Description => "View shopping list";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var pageNumber = ParsePageNumber(message.Text);
        var (messageText, keyboard, group) = await this.listService.BuildListAsync(message.Chat.Id, pageNumber);

        var sent = await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: messageText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        await this.groupRepository.UpdateListMessageIdAsync(group.Id, sent.MessageId);

        try
        {
            var (_, totalItems, totalPages, actualPageNumber) = await this.listService.GetPagedItemsAsync(group.Id, pageNumber, pageSize: 10);
            var payload = new ListViewedPayload(actualPageNumber, 10, totalItems);
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ListViewedPayload);
            await this.historyRepository.RecordAsync(
                chatId: message.Chat.Id,
                userId: message.From?.Id ?? 0,
                userName: message.From?.FirstName ?? message.From?.Username ?? "Неизвестный",
                actionType: BotActionType.ListViewed,
                payloadJson: payloadJson,
                revertPayloadJson: null,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ListViewed");
        }
    }

    private static int ParsePageNumber(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return 1;
        }

        var parts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return 1;
        }

        if (int.TryParse(parts[1], out var pageNumber) && pageNumber > 0)
        {
            return pageNumber;
        }

        return 1;
    }
}
