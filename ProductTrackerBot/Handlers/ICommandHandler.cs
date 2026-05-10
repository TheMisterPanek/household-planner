// <copyright file="ICommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Telegram.Bot.Types;

/// <summary>
/// Handles a specific Telegram bot command (e.g. /buy, /list).
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Gets the command string this handler responds to (e.g. "/buy").
    /// </summary>
    string Command { get; }

    /// <summary>
    /// Gets the optional command description shown in Telegram's suggestion list.
    /// Handlers returning <c>null</c> are excluded from <c>setMyCommands</c> and from the /start reply list.
    /// </summary>
    string? Description => null;

    /// <summary>
    /// Handles the command message.
    /// </summary>
    /// <param name="message">The incoming message containing the command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task HandleAsync(Message message, CancellationToken cancellationToken);
}
