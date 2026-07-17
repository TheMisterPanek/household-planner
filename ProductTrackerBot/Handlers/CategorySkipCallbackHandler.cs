// <copyright file="CategorySkipCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "Без категории" skip button on the category-capture prompt.
/// </summary>
public class CategorySkipCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<CategoryCaptureDialogState> dialogService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategorySkipCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The category-capture dialog state service.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public CategorySkipCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<CategoryCaptureDialogState> dialogService,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "category:skip";

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

        var promptText = this.localizer.Get(chatId, "category.prompt").Replace("{item}", state.ItemLabel);

        await this.botClient.EditMessageText(
            chatId: chatId,
            messageId: callbackQuery.Message.MessageId,
            text: $"{promptText}\n\n{this.localizer.Get(chatId, "category.skipped")}",
            cancellationToken: cancellationToken);
    }
}
