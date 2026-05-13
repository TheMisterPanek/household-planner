// <copyright file="ActionCancelCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

/// <summary>
/// Handles the "action:cancel" callback — deletes or clears the action message without executing the action.
/// </summary>
public class ActionCancelCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ILogger<ActionCancelCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCancelCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="logger">The logger.</param>
    public ActionCancelCallbackHandler(ITelegramBotClient botClient, ILogger<ActionCancelCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "action:cancel";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        try
        {
            await this.botClient.DeleteMessage(chatId, messageId, cancellationToken);
        }
        catch (ApiRequestException ex)
        {
            this.logger.LogInformation(ex, "Cannot delete message {MessageId} in chat {ChatId}; removing keyboard instead", messageId, chatId);
            await this.botClient.EditMessageReplyMarkup(chatId, messageId, replyMarkup: null, cancellationToken: cancellationToken);
        }

        await this.botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }
}
