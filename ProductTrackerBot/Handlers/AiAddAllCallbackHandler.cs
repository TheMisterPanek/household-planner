// <copyright file="AiAddAllCallbackHandler.cs" company="PlaceholderCompany">
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

/// <summary>
/// Handles the "➕ Add All" batch suggestion button emitted after an /ai response.
/// </summary>
public class AiAddAllCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly AiSuggestionService aiSuggestionService;
    private readonly GroupRepository groupRepository;
    private readonly ShoppingItemRepository itemRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<AiAddAllCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiAddAllCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="aiSuggestionService">The AI suggestion token store.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="logger">The logger.</param>
    public AiAddAllCallbackHandler(
        ITelegramBotClient botClient,
        AiSuggestionService aiSuggestionService,
        GroupRepository groupRepository,
        ShoppingItemRepository itemRepository,
        IHistoryRepository historyRepository,
        ILocalizer localizer,
        ILogger<AiAddAllCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.aiSuggestionService = aiSuggestionService;
        this.groupRepository = groupRepository;
        this.itemRepository = itemRepository;
        this.historyRepository = historyRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "ai:add-all:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var token = callbackQuery.Data["ai:add-all:".Length..];
        var suggestions = this.aiSuggestionService.GetBatch(token);

        if (suggestions is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: this.localizer.Get(chatId, "ai.suggestion-expired"),
                cancellationToken: cancellationToken);
            return;
        }

        this.aiSuggestionService.ClearBatch(token);

        var group = await this.groupRepository.GetOrCreateAsync(chatId);
        var addedByName = callbackQuery.From.FirstName ?? "Unknown";

        foreach (var suggestion in suggestions)
        {
            try
            {
                var item = await this.itemRepository.AddAsync(
                    groupId: group.Id,
                    name: suggestion.Name,
                    quantity: suggestion.Count,
                    addedByName: addedByName);

                var payload = new ItemPayload(item.Name, item.Quantity);
                var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
                await this.historyRepository.RecordAsync(
                    chatId: chatId,
                    userId: callbackQuery.From.Id,
                    userName: addedByName,
                    actionType: BotActionType.ItemAdded,
                    payloadJson: payloadJson,
                    revertPayloadJson: null,
                    ct: cancellationToken);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to add AI suggestion '{Name}' in batch for chat {ChatId}", suggestion.Name, chatId);
            }
        }

        var countStr = suggestions.Count.ToString();

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: this.localizer.Get(chatId, "ai.suggestion-all-added")
                .Replace("{count}", countStr, StringComparison.Ordinal),
            cancellationToken: cancellationToken);

        var msg = this.localizer.Get(chatId, "ai.suggestion-all-added-msg")
            .Replace("{count}", countStr, StringComparison.Ordinal);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: msg,
            cancellationToken: cancellationToken);
    }
}
