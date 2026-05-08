// <copyright file="IHistoryRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

using ProductTrackerBot.Models;

/// <summary>
/// Append-and-query abstraction over the <c>BotActionHistory</c> table.
/// </summary>
public interface IHistoryRepository
{
    /// <summary>
    /// Records a bot action in the history table.
    /// </summary>
    Task RecordAsync(
        long chatId,
        long userId,
        string userName,
        BotActionType actionType,
        string payloadJson,
        CancellationToken ct);

    /// <summary>
    /// Returns the most recent history entries for a chat, newest first.
    /// </summary>
    Task<IReadOnlyList<BotActionEntry>> GetRecentAsync(long chatId, int limit, CancellationToken ct);
}
