// <copyright file="TagCaptureStepHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles a free-text reply to the tag-capture prompt — the reply text is added to the pending
/// selection as an additional tag, without clearing any already-toggled suggestions.
/// </summary>
public class TagCaptureStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<TagCaptureDialogState> dialogService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagCaptureStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The tag-capture dialog state service.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public TagCaptureStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<TagCaptureDialogState> dialogService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
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

        var tag = message.Text.Trim();
        if (tag.Length == 0)
        {
            return;
        }

        state.SelectedTagNames.Add(tag);
        this.dialogService.SetState(chatId, userId, state);

        var isAmongSuggestions = state.TopTags?.Contains(tag, StringComparer.OrdinalIgnoreCase) ?? false;
        if (isAmongSuggestions)
        {
            var keyboard = TagCaptureService.BuildKeyboard(this.localizer, chatId, state);
            var promptText = this.localizer.Get(chatId, "tag.prompt").Replace("{item}", state.ItemLabel);

            await this.botClient.SendMessage(
                chatId: chatId,
                text: promptText,
                replyMarkup: keyboard,
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
        }
        else
        {
            var ackText = this.localizer.Get(chatId, "tag.free-text-added").Replace("{tag}", tag);

            await this.botClient.SendMessage(
                chatId: chatId,
                text: ackText,
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
        }
    }
}
