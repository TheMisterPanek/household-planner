// <copyright file="IDialogMessageHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Telegram.Bot.Types;

/// <summary>
/// Handles non-command text messages for ongoing multi-step dialogs.
/// </summary>
public interface IDialogMessageHandler
{
    /// <summary>
    /// Checks whether this handler can process a message for the given chat and user.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="userId">The Telegram user ID.</param>
    /// <returns>True if this handler should process the message.</returns>
    bool CanHandle(long chatId, long userId);

    /// <summary>
    /// Handles the dialog message.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task HandleAsync(Message message, CancellationToken cancellationToken);
}
