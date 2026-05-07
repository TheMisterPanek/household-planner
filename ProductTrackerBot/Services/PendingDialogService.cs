// <copyright file="PendingDialogService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Collections.Concurrent;

/// <summary>
/// Tracks in-memory dialog state keyed by (chatId, userId).
/// State is ephemeral and lost on restart — users retype the command.
/// </summary>
/// <typeparam name="TState">The dialog state type.</typeparam>
public class PendingDialogService<TState>
    where TState : class
{
    private readonly ConcurrentDictionary<(long ChatId, long UserId), TState> states = new();

    /// <summary>
    /// Sets the dialog state for a user in a chat.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="userId">The Telegram user ID.</param>
    /// <param name="state">The state to store.</param>
    public void SetState(long chatId, long userId, TState state)
    {
        this.states[(chatId, userId)] = state;
    }

    /// <summary>
    /// Gets the dialog state for a user in a chat, or null if not in a dialog.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="userId">The Telegram user ID.</param>
    /// <returns>The stored state, or null.</returns>
    public TState? GetState(long chatId, long userId)
    {
        this.states.TryGetValue((chatId, userId), out var state);
        return state;
    }

    /// <summary>
    /// Removes the dialog state, ending the dialog.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="userId">The Telegram user ID.</param>
    /// <returns>True if state was removed; false if none existed.</returns>
    public bool ClearState(long chatId, long userId)
    {
        return this.states.TryRemove((chatId, userId), out _);
    }
}
