// <copyright file="BotCommandRegistrationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Registers the bot's commands with Telegram at application startup.
/// </summary>
public class BotCommandRegistrationService : IHostedService
{
    private readonly ITelegramBotClient botClient;
    private readonly IEnumerable<ICommandHandler> commandHandlers;
    private readonly ILogger<BotCommandRegistrationService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotCommandRegistrationService"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="commandHandlers">The collection of command handlers.</param>
    /// <param name="logger">The logger.</param>
    public BotCommandRegistrationService(
        ITelegramBotClient botClient,
        IEnumerable<ICommandHandler> commandHandlers,
        ILogger<BotCommandRegistrationService> logger)
    {
        this.botClient = botClient;
        this.commandHandlers = commandHandlers;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var commands = this.commandHandlers
            .Where(h => h.Description is not null)
            .Select(h => new BotCommand
            {
                Command = h.Command.TrimStart('/'),
                Description = h.Description!,
            })
            .ToList();

        if (commands.Count == 0)
        {
            this.logger.LogInformation("No commands with descriptions found; skipping SetMyCommandsAsync");
            return;
        }

        try
        {
            await this.botClient.SetMyCommands(commands, cancellationToken: cancellationToken);
            this.logger.LogInformation("Successfully registered {CommandCount} commands with Telegram", commands.Count);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to register commands with Telegram");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
