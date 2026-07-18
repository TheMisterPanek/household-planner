// <copyright file="TagToggleCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles a tap on a suggested tag button during the tag-capture dialog — toggles membership in
/// the pending selection and re-renders the prompt's buttons in place, without closing the dialog.
/// </summary>
public class TagToggleCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<TagCaptureDialogState> dialogService;
    private readonly ILocalizer localizer;
    private readonly ILogger<TagToggleCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagToggleCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The tag-capture dialog state service.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public TagToggleCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<TagCaptureDialogState> dialogService,
        ILocalizer localizer,
        ILogger<TagToggleCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "tag:toggle:";

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

        if (state is null || state.TopTags is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Dialog expired, please try again",
                cancellationToken: cancellationToken);
            return;
        }

        var indexStr = callbackQuery.Data[this.CallbackPrefix.Length..];
        if (!int.TryParse(indexStr, out var index) || index < 0 || index >= state.TopTags.Count)
        {
            this.logger.LogWarning("Invalid tag index in callback: {Index}", indexStr);
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Invalid tag selection",
                cancellationToken: cancellationToken);
            return;
        }

        var tag = state.TopTags[index];
        if (!state.SelectedTagNames.Remove(tag))
        {
            state.SelectedTagNames.Add(tag);
        }

        this.dialogService.SetState(chatId, userId, state);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        var keyboard = TagCaptureService.BuildKeyboard(this.localizer, chatId, state);

        await this.botClient.EditMessageReplyMarkup(
            chatId: chatId,
            messageId: callbackQuery.Message.MessageId,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
