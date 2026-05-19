// <copyright file="UpdateDispatcher.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot;

using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<UpdateDispatcher> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDispatcher"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory used to create a DI scope per update.</param>
    /// <param name="logger">The logger.</param>
    public UpdateDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<UpdateDispatcher> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        using var scope = this.scopeFactory.CreateScope();
        var commandHandlers = scope.ServiceProvider.GetServices<ICommandHandler>();
        var callbackHandlers = scope.ServiceProvider.GetServices<ICallbackHandler>();
        var dialogHandlers = scope.ServiceProvider.GetServices<IDialogMessageHandler>();

        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await this.HandleMessageAsync(update.Message!, commandHandlers, dialogHandlers, cancellationToken);
                    break;

                case UpdateType.CallbackQuery:
                    await this.HandleCallbackQueryAsync(update.CallbackQuery!, callbackHandlers, cancellationToken);
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

    private async Task HandleMessageAsync(
        Message message,
        IEnumerable<ICommandHandler> commandHandlers,
        IEnumerable<IDialogMessageHandler> dialogHandlers,
        CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            this.logger.LogDebug("Received message without text from user {UserId}", message.From?.Id);
            return;
        }

        if (!message.Text.StartsWith('/'))
        {
            await this.TryHandleDialogMessageAsync(message, dialogHandlers, cancellationToken);
            return;
        }

        // Extract command (everything before first space or @)
        var command = message.Text.Split(' ', 2)[0];
        var atIndex = command.IndexOf('@', StringComparison.Ordinal);
        if (atIndex > 0)
        {
            command = command[..atIndex]; // Strip bot username suffix
        }

        var handler = FindCommandHandler(command, commandHandlers);
        if (handler is null)
        {
            this.logger.LogDebug("No command handler for command: {Command}", command);
            return;
        }

        this.logger.LogInformation("Dispatching command {Command} from user {UserId}", command, message.From?.Id);
        await handler.HandleAsync(message, cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(
        CallbackQuery callbackQuery,
        IEnumerable<ICallbackHandler> callbackHandlers,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null)
        {
            this.logger.LogDebug("Received callback query without data from user {UserId}", callbackQuery.From.Id);
            return;
        }

        var handler = FindCallbackHandler(callbackQuery.Data, callbackHandlers);
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

    private async Task TryHandleDialogMessageAsync(
        Message message,
        IEnumerable<IDialogMessageHandler> dialogHandlers,
        CancellationToken cancellationToken)
    {
        if (message.From is null)
        {
            return;
        }

        foreach (var handler in dialogHandlers)
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

    private static ICommandHandler? FindCommandHandler(string command, IEnumerable<ICommandHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            if (string.Equals(handler.Command, command, StringComparison.OrdinalIgnoreCase))
            {
                return handler;
            }
        }

        return null;
    }

    private static ICallbackHandler? FindCallbackHandler(string data, IEnumerable<ICallbackHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            if (data.StartsWith(handler.CallbackPrefix, StringComparison.Ordinal))
            {
                return handler;
            }
        }

        return null;
    }
}
