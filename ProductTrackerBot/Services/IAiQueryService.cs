// <copyright file="IAiQueryService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using ProductTrackerBot.Models;

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
    /// <param name="recentContext">Recent conversation turns (last 15 min, ≤500 chars) for short-term memory. Empty string if none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The answer text (cleaned of markers) and any item suggestions extracted from the AI response.</returns>
    Task<AiQueryResult> AnswerAsync(long chatId, long groupId, string question, string recentContext, CancellationToken ct);
}
