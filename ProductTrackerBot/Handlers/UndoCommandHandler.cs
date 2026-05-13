// <copyright file="UndoCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the /undo command — reverts the most recent reversible action for the calling user.
/// </summary>
public class UndoCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly IUndoService undoService;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UndoCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="undoService">The undo service.</param>
    /// <param name="localizer">The localizer.</param>
    public UndoCommandHandler(ITelegramBotClient botClient, IUndoService undoService, ILocalizer localizer)
    {
        this.botClient = botClient;
        this.undoService = undoService;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string Command => "/undo";

    /// <inheritdoc/>
    public string? Description => "Undo the last action";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;

        var resultKey = await this.undoService.UndoLastAsync(chatId, userId, cancellationToken);

        await this.botClient.SendMessage(
            chatId: chatId,
            text: this.localizer.Get(chatId, resultKey),
            cancellationToken: cancellationToken);
    }
}
