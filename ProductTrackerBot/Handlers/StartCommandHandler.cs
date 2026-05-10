// <copyright file="StartCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.DependencyInjection;
using ProductTrackerBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the /start command — sends a welcome message and a list of available commands.
/// </summary>
public class StartCommandHandler : ICommandHandler
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ITelegramBotClient botClient;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartCommandHandler"/> class.
    /// </summary>
    /// <param name="scopeFactory">Used to resolve the handler list without a circular dependency.</param>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public StartCommandHandler(
        IServiceScopeFactory scopeFactory,
        ITelegramBotClient botClient,
        ILocalizer localizer)
    {
        this.scopeFactory = scopeFactory;
        this.botClient = botClient;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string Command => "/start";

    /// <inheritdoc/>
    public string? Description => null;

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        using var scope = this.scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetRequiredService<IEnumerable<ICommandHandler>>();

        var publicCommands = handlers
            .Where(h => h.Description is not null)
            .ToList();

        var chatId = message.Chat.Id;
        var reply = publicCommands.Count == 0
            ? this.localizer.Get(chatId, "start.no-commands")
            : this.BuildWelcomeMessage(chatId, publicCommands);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: reply,
            cancellationToken: cancellationToken);
    }

    private string BuildWelcomeMessage(long chatId, List<ICommandHandler> publicCommands)
    {
        var header = this.localizer.Get(chatId, "start.welcome");
        var lines = new List<string> { header };

        foreach (var handler in publicCommands)
        {
            lines.Add($"{handler.Command} — {handler.Description}");
        }

        return string.Join('\n', lines);
    }
}
