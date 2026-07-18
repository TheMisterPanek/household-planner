// <copyright file="TagCaptureService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Starts the multi-select tag-capture follow-up dialog after item(s) are added or edited.
/// </summary>
public class TagCaptureService
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<TagCaptureDialogState> dialogService;
    private readonly PendingDialogService<PriceCaptureDialogState> priceDialogService;
    private readonly TagRepository tagRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagCaptureService"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The tag-capture dialog state service.</param>
    /// <param name="priceDialogService">The price-capture dialog state service, cleared when a tag-capture prompt starts so a stale price dialog cannot steal the reply.</param>
    /// <param name="tagRepository">The tag repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public TagCaptureService(
        ITelegramBotClient botClient,
        PendingDialogService<TagCaptureDialogState> dialogService,
        PendingDialogService<PriceCaptureDialogState> priceDialogService,
        TagRepository tagRepository,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.priceDialogService = priceDialogService;
        this.tagRepository = tagRepository;
        this.localizer = localizer;
    }

    /// <summary>
    /// Starts the tag-capture follow-up: fetches suggestions, stores dialog state (overwriting any
    /// still-pending prompt for the same chat/user), and sends the toggleable prompt message.
    /// Also clears any still-pending price-capture dialog for the same chat/user, since only one
    /// dialog can be waiting for the next text reply at a time — otherwise a stale price prompt
    /// (e.g. from marking a previous item bought) would intercept the reply meant for this prompt.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="userId">The Telegram user ID.</param>
    /// <param name="groupId">The group ID.</param>
    /// <param name="itemIds">The item ID(s) this prompt applies to.</param>
    /// <param name="itemLabel">The display text for the prompt (item name, or a pluralized count label for bulk).</param>
    /// <param name="preselectedTags">Tags already on the item(s), shown pre-toggled (e.g. on edit re-entry).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task StartTagCaptureAsync(
        long chatId,
        long userId,
        int groupId,
        IReadOnlyList<int> itemIds,
        string itemLabel,
        IReadOnlyCollection<string>? preselectedTags,
        CancellationToken cancellationToken)
    {
        var topTags = await this.tagRepository.GetTopTagsAsync(groupId, 5);

        var state = new TagCaptureDialogState
        {
            ItemIds = itemIds.ToList(),
            ItemLabel = itemLabel,
            GroupId = groupId,
            TopTags = topTags.Count > 0 ? new List<string>(topTags) : null,
            SelectedTagNames = new HashSet<string>(preselectedTags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
        };
        this.priceDialogService.ClearState(chatId, userId);
        this.dialogService.SetState(chatId, userId, state);

        var keyboard = BuildKeyboard(this.localizer, chatId, state);

        var promptText = this.localizer.Get(chatId, "tag.prompt").Replace("{item}", itemLabel);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: promptText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Builds the toggle-suggestion keyboard for a tag-capture prompt, reflecting the current
    /// <see cref="TagCaptureDialogState.SelectedTagNames"/> selection.
    /// </summary>
    /// <param name="localizer">The localizer.</param>
    /// <param name="chatId">The chat ID (for localization).</param>
    /// <param name="state">The current dialog state.</param>
    /// <returns>The built keyboard.</returns>
    internal static InlineKeyboardMarkup BuildKeyboard(ILocalizer localizer, long chatId, TagCaptureDialogState state)
    {
        var rows = new List<InlineKeyboardButton[]>();

        var topTags = state.TopTags ?? new List<string>();
        for (int i = 0; i < topTags.Count; i++)
        {
            var isSelected = state.SelectedTagNames.Contains(topTags[i]);
            var label = ShoppingListService.TruncateCategoryLabel(topTags[i]);
            if (isSelected)
            {
                label = $"✓ {label}";
            }

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(label, $"tag:toggle:{i}"),
            });
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(localizer.Get(chatId, "category.skip"), "tag:skip"),
        });

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(localizer.Get(chatId, "tag.done"), "tag:done"),
        });

        return new InlineKeyboardMarkup(rows);
    }
}
