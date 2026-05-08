// <copyright file="MealMergeService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Collections.Concurrent;
using ProductTrackerBot.Models;

/// <summary>
/// Manages in-memory merge sessions for resolving ingredient conflicts.
/// </summary>
public class MealMergeService
{
    private static readonly Random Random = new();
    private readonly ConcurrentDictionary<string, MergeSession> sessions = new();
    private readonly TimeSpan sessionTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Creates a new merge session.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="mealId">The meal ID.</param>
    /// <param name="conflicts">The list of conflicts.</param>
    /// <returns>An 8-character alphanumeric session key.</returns>
    public string CreateSession(long chatId, int mealId, List<MergeConflict> conflicts)
    {
        var sessionId = GenerateSessionKey();
        var session = new MergeSession
        {
            ChatId = chatId,
            MealId = mealId,
            CreatedAt = DateTime.UtcNow,
            Conflicts = conflicts,
        };

        this.sessions.TryAdd(sessionId, session);
        return sessionId;
    }

    /// <summary>
    /// Tries to get a session by key, with lazy purge of expired sessions.
    /// </summary>
    /// <param name="sessionId">The session key.</param>
    /// <returns>The session, or null if not found or expired.</returns>
    public MergeSession? TryGetSession(string sessionId)
    {
        this.PurgeExpiredSessions();

        if (this.sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        return null;
    }

    /// <summary>
    /// Resolves a conflict in a session.
    /// </summary>
    /// <param name="sessionId">The session key.</param>
    /// <param name="index">The conflict index.</param>
    /// <param name="add">True if adding anyway, false if skipping.</param>
    public void ResolveConflict(string sessionId, int index, bool add)
    {
        if (this.sessions.TryGetValue(sessionId, out var session))
        {
            session.Resolved[index] = add;
        }
    }

    private static string GenerateSessionKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[8];
        for (int i = 0; i < 8; i++)
        {
            result[i] = chars[Random.Next(chars.Length)];
        }

        return new string(result);
    }

    private void PurgeExpiredSessions()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = this.sessions
            .Where(kvp => now - kvp.Value.CreatedAt > this.sessionTimeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            this.sessions.TryRemove(key, out _);
        }
    }
}
