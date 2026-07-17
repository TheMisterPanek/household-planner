// <copyright file="CategoryCaptureService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Starts the category-capture follow-up dialog after item(s) are added or edited.
/// </summary>
public class CategoryCaptureService
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<CategoryCaptureDialogState> dialogService;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryCaptureService"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The category-capture dialog state service.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public CategoryCaptureService(
        ITelegramBotClient botClient,
        PendingDialogService<CategoryCaptureDialogState> dialogService,
        PurchaseHistoryRepository purchaseRepository,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.purchaseRepository = purchaseRepository;
        this.localizer = localizer;
    }

    /// <summary>
    /// Starts the category-capture follow-up: fetches suggestions, stores dialog state, and sends the prompt.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="userId">The Telegram user ID.</param>
    /// <param name="groupId">The group ID.</param>
    /// <param name="itemIds">The item ID(s) this prompt applies to.</param>
    /// <param name="itemLabel">The display text for the prompt (item name, or a pluralized count label for bulk).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task StartCategoryCaptureAsync(
        long chatId,
        long userId,
        int groupId,
        IReadOnlyList<int> itemIds,
        string itemLabel,
        CancellationToken cancellationToken)
    {
        var topCategories = await this.purchaseRepository.GetTopCategoriesAsync(groupId, 5);

        var state = new CategoryCaptureDialogState
        {
            ItemIds = itemIds.ToList(),
            ItemLabel = itemLabel,
            GroupId = groupId,
            TopCategories = topCategories.Count > 0 ? new List<string>(topCategories) : null,
        };
        this.dialogService.SetState(chatId, userId, state);

        var keyboard = BuildKeyboard(this.localizer, chatId, topCategories);

        var promptText = this.localizer.Get(chatId, "category.prompt").Replace("{item}", itemLabel);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: promptText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private static InlineKeyboardMarkup BuildKeyboard(ILocalizer localizer, long chatId, IReadOnlyList<string> topCategories)
    {
        var rows = new List<InlineKeyboardButton[]>();

        for (int i = 0; i < topCategories.Count; i++)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    ShoppingListService.TruncateCategoryLabel(topCategories[i]),
                    $"category:suggest:{i}"),
            });
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(localizer.Get(chatId, "category.skip"), "category:skip"),
        });

        return new InlineKeyboardMarkup(rows);
    }
}
