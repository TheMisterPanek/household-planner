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
        string? revertPayloadJson,
        CancellationToken ct);

    /// <summary>
    /// Returns the most recent history entries for a chat, newest first.
    /// </summary>
    Task<IReadOnlyList<BotActionEntry>> GetRecentAsync(long chatId, int limit, CancellationToken ct);

    /// <summary>
    /// Returns the most recent reversible entry for the given chat+user, or null if none exists.
    /// </summary>
    Task<BotActionEntry?> GetLatestReversibleAsync(long chatId, long userId, CancellationToken ct);

    /// <summary>
    /// Sets <c>reverted_at</c> on the specified history row.
    /// </summary>
    Task MarkRevertedAsync(long entryId, string revertedAt, CancellationToken ct);
}
