// <copyright file="ICallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Telegram.Bot.Types;

/// <summary>
/// Handles a Telegram callback query matching a specific data prefix.
/// </summary>
public interface ICallbackHandler
{
    /// <summary>
    /// Gets the callback data prefix this handler responds to (e.g. "shop:done:").
    /// </summary>
    string CallbackPrefix { get; }

    /// <summary>
    /// Handles the callback query.
    /// </summary>
    /// <param name="callbackQuery">The incoming callback query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken);
}
