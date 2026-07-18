// <copyright file="TagSkipCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "Без категории" skip button on the tag-capture prompt — clears the dialog without
/// applying any tags.
/// </summary>
public class TagSkipCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<TagCaptureDialogState> dialogService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagSkipCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The tag-capture dialog state service.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public TagSkipCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<TagCaptureDialogState> dialogService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "tag:skip";

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

        this.dialogService.ClearState(chatId, userId);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        var promptText = this.localizer.Get(chatId, "tag.prompt").Replace("{item}", state.ItemLabel);

        await this.botClient.EditMessageText(
            chatId: chatId,
            messageId: callbackQuery.Message.MessageId,
            text: $"{promptText}\n\n{this.localizer.Get(chatId, "category.skipped")}",
            cancellationToken: cancellationToken);
    }
}
