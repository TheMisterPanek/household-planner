// <copyright file="ConversationHistoryService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Collections.Concurrent;

/// <summary>
/// Holds recent per-chat AI conversation turns (last 15 min, up to 500 chars) to give the AI short-term memory.
/// </summary>
public class ConversationHistoryService
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int MaxChars = 500;

    private readonly ConcurrentDictionary<long, List<(DateTime At, string Text)>> history = new();

    /// <summary>Records a conversation turn for the given chat.</summary>
    public void Record(long chatId, string text)
    {
        var entries = this.history.GetOrAdd(chatId, _ => new List<(DateTime, string)>());
        lock (entries)
        {
            entries.Add((DateTime.UtcNow, text));
        }
    }

    /// <summary>
    /// Returns the recent conversation context for the given chat: entries from the last 15 minutes,
    /// joined by newlines, truncated to the last 500 characters.
    /// </summary>
    public string GetRecentContext(long chatId)
    {
        if (!this.history.TryGetValue(chatId, out var entries))
        {
            return string.Empty;
        }

        var cutoff = DateTime.UtcNow - Window;
        lock (entries)
        {
            entries.RemoveAll(e => e.At < cutoff);
            if (entries.Count == 0)
            {
                return string.Empty;
            }

            var full = string.Join("\n", entries.Select(e => e.Text));
            return full.Length <= MaxChars ? full : full[^MaxChars..];
        }
    }
}
