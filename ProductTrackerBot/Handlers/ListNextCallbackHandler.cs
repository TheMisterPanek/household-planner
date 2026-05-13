// <copyright file="ListNextCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the list next page navigation callback.
/// </summary>
public class ListNextCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ShoppingListService listService;
    private readonly GroupRepository groupRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly ILogger<ListNextCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListNextCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="listService">The shopping list service.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="logger">The logger.</param>
    public ListNextCallbackHandler(
        ITelegramBotClient botClient,
        ShoppingListService listService,
        GroupRepository groupRepository,
        IHistoryRepository historyRepository,
        ILogger<ListNextCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.listService = listService;
        this.groupRepository = groupRepository;
        this.historyRepository = historyRepository;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "list_next:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var data = callbackQuery.Data["list_next:".Length..];
        var parts = data.Split(':');
        if (parts.Length != 2 || !long.TryParse(parts[0], out var groupChatId) || !int.TryParse(parts[1], out var pageNumber))
        {
            this.logger.LogWarning("Invalid data in list_next callback: {Data}", data);
            return;
        }

        var group = await this.groupRepository.GetOrCreateAsync(groupChatId);
        var (messageText, keyboard, _) = await this.listService.BuildListAsync(groupChatId, pageNumber);

        try
        {
            await this.botClient.EditMessageText(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 400)
        {
            var sent = await this.botClient.SendMessage(
                chatId: callbackQuery.Message.Chat.Id,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);

            await this.groupRepository.UpdateListMessageIdAsync(group.Id, sent.MessageId);
        }

        try
        {
            var (_, totalItems, totalPages, actualPageNumber) = await this.listService.GetPagedItemsAsync(group.Id, pageNumber, pageSize: 10);
            var payload = new ListViewedPayload(actualPageNumber, 10, totalItems);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ListViewedPayload);
            await this.historyRepository.RecordAsync(
                chatId: callbackQuery.Message.Chat.Id,
                userId: callbackQuery.From.Id,
                userName: callbackQuery.From.FirstName ?? "Неизвестный",
                actionType: BotActionType.ListViewed,
                payloadJson: payloadJson,
                revertPayloadJson: null,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ListViewed");
        }

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);
    }
}
