// <copyright file="StartCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the /start command — sends a welcome message and a list of available commands.
/// </summary>
public class StartCommandHandler : ICommandHandler
{
    private readonly IEnumerable<ICommandHandler> commandHandlers;
    private readonly ITelegramBotClient botClient;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartCommandHandler"/> class.
    /// </summary>
    /// <param name="commandHandlers">The collection of command handlers.</param>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public StartCommandHandler(
        IEnumerable<ICommandHandler> commandHandlers,
        ITelegramBotClient botClient,
        ILocalizer localizer)
    {
        this.commandHandlers = commandHandlers;
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
        var publicCommands = this.commandHandlers
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
