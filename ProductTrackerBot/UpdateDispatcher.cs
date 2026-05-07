// <copyright file="UpdateDispatcher.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot;

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

/// <summary>
/// Dispatches Telegram updates to matching <see cref="ICommandHandler"/> or <see cref="ICallbackHandler"/> implementations.
/// </summary>
public class UpdateDispatcher : IUpdateHandler
{
    private readonly IEnumerable<ICommandHandler> commandHandlers;
    private readonly IEnumerable<ICallbackHandler> callbackHandlers;
    private readonly IEnumerable<IDialogMessageHandler> dialogHandlers;
    private readonly ILogger<UpdateDispatcher> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDispatcher"/> class.
    /// </summary>
    /// <param name="commandHandlers">All registered command handlers.</param>
    /// <param name="callbackHandlers">All registered callback handlers.</param>
    /// <param name="logger">The logger.</param>
    public UpdateDispatcher(
        IEnumerable<ICommandHandler> commandHandlers,
        IEnumerable<ICallbackHandler> callbackHandlers,
        IEnumerable<IDialogMessageHandler> dialogHandlers,
        ILogger<UpdateDispatcher> logger)
    {
        this.commandHandlers = commandHandlers;
        this.callbackHandlers = callbackHandlers;
        this.dialogHandlers = dialogHandlers;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await this.HandleMessageAsync(update.Message!, cancellationToken);
                    break;

                case UpdateType.CallbackQuery:
                    await this.HandleCallbackQueryAsync(update.CallbackQuery!, cancellationToken);
                    break;

                default:
                    this.logger.LogDebug("Received unhandled update type: {UpdateType}", update.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    /// <inheritdoc/>
    public async Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        this.logger.LogError(exception, "Error while processing updates from source: {Source}", source);
        await Task.CompletedTask;
    }

    private async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            this.logger.LogDebug("Received message without text from user {UserId}", message.From?.Id);
            return;
        }

        if (!message.Text.StartsWith('/'))
        {
            // Check for pending dialogs
            await this.TryHandleDialogMessageAsync(message, cancellationToken);
            return;
        }

        // Extract command (everything before first space or @)
        var command = message.Text.Split(' ', 2)[0];
        var atIndex = command.IndexOf('@', StringComparison.Ordinal);
        if (atIndex > 0)
        {
            command = command[..atIndex]; // Strip bot username suffix
        }

        var handler = this.FindCommandHandler(command);
        if (handler is null)
        {
            this.logger.LogDebug("No command handler for command: {Command}", command);
            return;
        }

        this.logger.LogInformation("Dispatching command {Command} from user {UserId}", command, message.From?.Id);
        await handler.HandleAsync(message, cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null)
        {
            this.logger.LogDebug("Received callback query without data from user {UserId}", callbackQuery.From.Id);
            return;
        }

        var handler = this.FindCallbackHandler(callbackQuery.Data);
        if (handler is null)
        {
            this.logger.LogDebug("No callback handler for data prefix: {Data}", callbackQuery.Data);
            return;
        }

        this.logger.LogInformation(
            "Dispatching callback {Data} from user {UserId}",
            callbackQuery.Data,
            callbackQuery.From.Id);

        await handler.HandleAsync(callbackQuery, cancellationToken);
    }

    private async Task TryHandleDialogMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            return;
        }

        foreach (var handler in this.dialogHandlers)
        {
            if (handler.CanHandle(message.Chat.Id, message.From.Id))
            {
                this.logger.LogInformation(
                    "Dispatching dialog message from user {UserId}",
                    message.From.Id);
                await handler.HandleAsync(message, cancellationToken);
                return;
            }
        }

        this.logger.LogDebug("No dialog handler for message from user {UserId}", message.From.Id);
    }

    private ICommandHandler? FindCommandHandler(string command)
    {
        foreach (var handler in this.commandHandlers)
        {
            if (string.Equals(handler.Command, command, StringComparison.OrdinalIgnoreCase))
            {
                return handler;
            }
        }

        return null;
    }

    private ICallbackHandler? FindCallbackHandler(string data)
    {
        foreach (var handler in this.callbackHandlers)
        {
            if (data.StartsWith(handler.CallbackPrefix, StringComparison.Ordinal))
            {
                return handler;
            }
        }

        return null;
    }
}
