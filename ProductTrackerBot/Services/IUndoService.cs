// <copyright file="IUndoService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

/// <summary>
/// Applies the inverse of the most recent reversible action for a user in a chat.
/// </summary>
public interface IUndoService
{
    /// <summary>
    /// Undoes the last reversible action for the given chat+user.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="userId">The Telegram user ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A localization key describing the outcome ("undo.success", "undo.nothing", or "undo.error").</returns>
    Task<string> UndoLastAsync(long chatId, long userId, CancellationToken ct);
}
