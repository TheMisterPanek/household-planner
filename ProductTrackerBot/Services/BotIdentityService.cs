// <copyright file="BotIdentityService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

/// <summary>
/// Resolves and stores the bot's own Telegram username at startup via <c>GetMeAsync</c>.
/// </summary>
public class BotIdentityService : IHostedService
{
    private readonly ITelegramBotClient botClient;
    private readonly ILogger<BotIdentityService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotIdentityService"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="logger">The logger.</param>
    public BotIdentityService(ITelegramBotClient botClient, ILogger<BotIdentityService> logger)
    {
        this.botClient = botClient;
        this.logger = logger;
    }

    /// <summary>Gets the bot's username (without the leading @), resolved on startup.</summary>
    public string BotUsername { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var me = await this.botClient.GetMe(cancellationToken);
        this.BotUsername = me.Username ?? string.Empty;
        this.logger.LogInformation("Bot username resolved: @{Username}", this.BotUsername);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
