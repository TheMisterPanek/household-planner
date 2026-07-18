// <copyright file="TagDoneCallbackHandler.cs" company="PlaceholderCompany">
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
/// Handles the "Готово" button on the tag-capture prompt — applies the accumulated selection and
/// clears the dialog.
/// </summary>
public class TagDoneCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<TagCaptureDialogState> dialogService;
    private readonly TagRepository tagRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagDoneCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The tag-capture dialog state service.</param>
    /// <param name="tagRepository">The tag repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public TagDoneCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<TagCaptureDialogState> dialogService,
        TagRepository tagRepository,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.tagRepository = tagRepository;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "tag:done";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var userId = callbackQuery.From.Id;
        var state = this.dialogService.GetState(chatId, userId);

        if (state is null)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Dialog expired, please try again",
                cancellationToken: cancellationToken);
            return;
        }

        await this.tagRepository.SetItemTagsAsync(state.ItemIds, state.GroupId, state.SelectedTagNames);

        this.dialogService.ClearState(chatId, userId);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        var confirmText = state.SelectedTagNames.Count > 0
            ? this.localizer.Get(chatId, "tag.set-confirmation").Replace("{tags}", string.Join(", ", state.SelectedTagNames))
            : this.localizer.Get(chatId, "tag.set-confirmation-empty");

        await this.botClient.EditMessageText(
            chatId: chatId,
            messageId: callbackQuery.Message.MessageId,
            text: confirmText,
            cancellationToken: cancellationToken);
    }
}
