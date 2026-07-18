// <copyright file="TagDoneCallbackHandler.cs" company="PlaceholderCompany">
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
/// Handles the "Готово" button on the tag-capture prompt — applies the accumulated selection and
/// clears the dialog.
/// </summary>
public class TagDoneCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<TagCaptureDialogState> dialogService;
    private readonly TagRepository tagRepository;
    private readonly ShoppingListService listService;
    private readonly ILocalizer localizer;
    private readonly ILogger<TagDoneCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagDoneCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The tag-capture dialog state service.</param>
    /// <param name="tagRepository">The tag repository.</param>
    /// <param name="listService">The shopping list service, used to refresh the list message so the newly-tagged item(s) reflect their tags without waiting for the next unrelated list refresh.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public TagDoneCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<TagCaptureDialogState> dialogService,
        TagRepository tagRepository,
        ShoppingListService listService,
        ILocalizer localizer,
        ILogger<TagDoneCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.tagRepository = tagRepository;
        this.listService = listService;
        this.localizer = localizer;
        this.logger = logger;
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

        await this.RefreshListMessageAsync(chatId, cancellationToken);
    }

    private async Task RefreshListMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        var (messageText, keyboard, _) = await this.listService.BuildListAsync(chatId);
        try
        {
            await this.botClient.SendMessage(
                chatId: chatId,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to refresh list after tag capture");
        }
    }
}
