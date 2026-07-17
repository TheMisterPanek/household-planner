// <copyright file="CategorySuggestCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles a tap on a suggested category button during the category-capture dialog.
/// </summary>
public class CategorySuggestCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<CategoryCaptureDialogState> dialogService;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<CategorySuggestCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategorySuggestCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The category-capture dialog state service.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public CategorySuggestCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<CategoryCaptureDialogState> dialogService,
        ShoppingItemRepository itemRepository,
        ILocalizer localizer,
        ILogger<CategorySuggestCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.itemRepository = itemRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "category:suggest:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var userId = callbackQuery.From.Id;
        var state = this.dialogService.GetState(chatId, userId);

        if (state is null || state.TopCategories is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Dialog expired, please try again",
                cancellationToken: cancellationToken);
            return;
        }

        var indexStr = callbackQuery.Data[this.CallbackPrefix.Length..];
        if (!int.TryParse(indexStr, out var index) || index < 0 || index >= state.TopCategories.Count)
        {
            this.logger.LogWarning("Invalid category index in callback: {Index}", indexStr);
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Invalid category selection",
                cancellationToken: cancellationToken);
            return;
        }

        var selectedCategory = state.TopCategories[index];

        await this.itemRepository.UpdateCategoryAsync(state.ItemIds, selectedCategory);

        this.dialogService.ClearState(chatId, userId);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        var confirmText = this.localizer.Get(chatId, "category.set-confirmation").Replace("{category}", selectedCategory);

        await this.botClient.EditMessageText(
            chatId: chatId,
            messageId: callbackQuery.Message.MessageId,
            text: confirmText,
            cancellationToken: cancellationToken);
    }
}
