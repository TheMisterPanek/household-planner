// <copyright file="TagSkipCallbackHandler.cs" company="PlaceholderCompany">
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
/// Handles the "Без категории" skip button on the tag-capture prompt — clears the dialog without
/// applying any tags.
/// </summary>
public class TagSkipCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<TagCaptureDialogState> dialogService;
    private readonly ShoppingListService listService;
    private readonly ILocalizer localizer;
    private readonly ILogger<TagSkipCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagSkipCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The tag-capture dialog state service.</param>
    /// <param name="listService">The shopping list service, used to refresh the list message so the newly-added item shows up without waiting for the next unrelated list refresh.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public TagSkipCallbackHandler(
        ITelegramBotClient botClient,
        PendingDialogService<TagCaptureDialogState> dialogService,
        ShoppingListService listService,
        ILocalizer localizer,
        ILogger<TagSkipCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.listService = listService;
        this.localizer = localizer;
        this.logger = logger;
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
            this.logger.LogWarning(ex, "Failed to refresh list after tag skip");
        }
    }
}
