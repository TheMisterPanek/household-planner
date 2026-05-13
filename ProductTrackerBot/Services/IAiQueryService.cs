// <copyright file="IAiQueryService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

/// <summary>
/// Translates a natural language question into SQL, executes it read-only, and returns a
/// formatted natural language answer using a two-round AI interaction.
/// </summary>
public interface IAiQueryService
{
    /// <summary>
    /// Answers a natural language question about the chat's shopping data.
    /// </summary>
    /// <param name="chatId">Telegram chat ID (for localizing error messages).</param>
    /// <param name="groupId">Internal database GroupId scoping all queries.</param>
    /// <param name="question">The user's natural language question.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A natural language answer, or a localized error message on failure.</returns>
    Task<string> AnswerAsync(long chatId, long groupId, string question, CancellationToken ct);
}
