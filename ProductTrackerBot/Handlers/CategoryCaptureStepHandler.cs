// <copyright file="CategoryCaptureStepHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles a free-text reply to the category-capture prompt — the reply text becomes the category,
/// creating a new one if it doesn't already exist.
/// </summary>
public class CategoryCaptureStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<CategoryCaptureDialogState> dialogService;
    private readonly ShoppingItemRepository itemRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryCaptureStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The category-capture dialog state service.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public CategoryCaptureStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<CategoryCaptureDialogState> dialogService,
        ShoppingItemRepository itemRepository,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.itemRepository = itemRepository;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public bool CanHandle(long chatId, long userId)
    {
        var state = this.dialogService.GetState(chatId, userId);
        return state is not null;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null || message.Text is null)
        {
            return;
        }

        var chatId = message.Chat.Id;
        var userId = message.From.Id;
        var state = this.dialogService.GetState(chatId, userId);
        if (state is null)
        {
            return;
        }

        var category = message.Text.Trim();

        await this.itemRepository.UpdateCategoryAsync(state.ItemIds, category);

        this.dialogService.ClearState(chatId, userId);

        var confirmText = this.localizer.Get(chatId, "category.set-confirmation").Replace("{category}", category);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: confirmText,
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }
}
