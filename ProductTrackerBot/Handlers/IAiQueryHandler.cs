// <copyright file="IAiQueryHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Telegram.Bot.Types;

/// <summary>
/// Exposes the AI question-answering logic so it can be invoked from both the
/// <c>/ai</c> command handler and the bot-mention routing in <see cref="UpdateDispatcher"/>.
/// </summary>
public interface IAiQueryHandler
{
    /// <summary>
    /// Handles an already-extracted question string, sending the answer back to the chat.
    /// </summary>
    /// <param name="message">The originating Telegram message (for chat ID and reply context).</param>
    /// <param name="question">The natural language question (may be empty).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task HandleQueryAsync(Message message, string question, CancellationToken cancellationToken);
}
