// <copyright file="ListFilterCallbackHandler.cs" company="PlaceholderCompany">
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
/// Handles taps on the /list tag filter row — re-renders the list scoped to the active tag set, or clears the filter.
/// </summary>
public class ListFilterCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ShoppingListService listService;
    private readonly GroupRepository groupRepository;
    private readonly TagRepository tagRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly ILogger<ListFilterCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListFilterCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="listService">The shopping list service.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="tagRepository">The tag repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="logger">The logger.</param>
    public ListFilterCallbackHandler(
        ITelegramBotClient botClient,
        ShoppingListService listService,
        GroupRepository groupRepository,
        TagRepository tagRepository,
        IHistoryRepository historyRepository,
        ILogger<ListFilterCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.listService = listService;
        this.groupRepository = groupRepository;
        this.tagRepository = tagRepository;
        this.historyRepository = historyRepository;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "list_filter:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var data = callbackQuery.Data[this.CallbackPrefix.Length..];
        var parts = data.Split(':');
        if (parts.Length != 3
            || !long.TryParse(parts[0], out var groupChatId)
            || !int.TryParse(parts[2], out var pageNumber))
        {
            this.logger.LogWarning("Invalid data in list_filter callback: {Data}", data);
            return;
        }

        var group = await this.groupRepository.GetOrCreateAsync(groupChatId);

        var tagIndices = parts[1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var idx) ? idx : -1)
            .Where(idx => idx >= 0)
            .ToList();

        IReadOnlyCollection<string>? tagNames = null;
        if (tagIndices.Count > 0)
        {
            var allTags = await this.tagRepository.GetDistinctTagsAsync(group.Id);
            var resolved = tagIndices
                .Where(idx => idx < allTags.Count)
                .Select(idx => allTags[idx])
                .ToList();
            if (resolved.Count > 0)
            {
                tagNames = resolved;
            }
        }

        var (messageText, keyboard, _) = await this.listService.BuildListAsync(groupChatId, pageNumber, tagNames);

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
            // Telegram returns 400 both when the message can't be found and when the edit is a no-op
            // ("message is not modified", e.g. re-tapping the already-active filter). Only the former
            // needs a fresh message; resending on a no-op would post a duplicate list.
            if (!ex.Message.Contains("not modified", StringComparison.OrdinalIgnoreCase))
            {
                var sent = await this.botClient.SendMessage(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: messageText,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);

                await this.groupRepository.UpdateListMessageIdAsync(group.Id, sent.MessageId);
            }
        }

        try
        {
            var (_, totalItems, totalPages, actualPageNumber) = await this.listService.GetPagedItemsAsync(group.Id, pageNumber, pageSize: 10, tagNames);
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
