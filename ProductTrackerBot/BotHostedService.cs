// <copyright file="BotHostedService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;

/// <summary>
/// Background service that runs the Telegram bot polling loop.
/// </summary>
public class BotHostedService : BackgroundService
{
    private readonly ITelegramBotClient botClient;
    private readonly IUpdateHandler updateHandler;
    private readonly ILogger<BotHostedService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotHostedService"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="updateHandler">The update handler.</param>
    /// <param name="logger">The logger.</param>
    public BotHostedService(
        ITelegramBotClient botClient,
        IUpdateHandler updateHandler,
        ILogger<BotHostedService> logger)
    {
        this.botClient = botClient;
        this.updateHandler = updateHandler;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Bot polling stopped");
        await base.StopAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("Bot polling started");

        var receiverOptions = new ReceiverOptions();

        this.botClient.StartReceiving(
            updateHandler: this.updateHandler,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken);

        // Keep the service running until cancellation is requested
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }
}
