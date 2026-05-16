// <copyright file="AiSuggestionService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using ProductTrackerBot.Models;

/// <summary>
/// Holds <see cref="AiSuggestion"/> records keyed by a short random token until tapped or expired.
/// </summary>
public sealed class AiSuggestionService
{
    private readonly ConcurrentDictionary<string, AiSuggestion> pending = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<AiSuggestion>> pendingBatches = new();

    /// <summary>
    /// Stores a suggestion and returns an 8-character lowercase hex token.
    /// </summary>
    /// <param name="suggestion">The suggestion to store.</param>
    /// <returns>The token used to retrieve or clear the suggestion.</returns>
    public string Store(AiSuggestion suggestion)
    {
        var token = GenerateToken();
        this.pending[token] = suggestion;
        return token;
    }

    /// <summary>
    /// Returns the suggestion for the given token, or null if not found.
    /// </summary>
    /// <param name="token">The session token.</param>
    /// <returns>The suggestion, or null.</returns>
    public AiSuggestion? Get(string token) => this.pending.GetValueOrDefault(token);

    /// <summary>
    /// Removes the suggestion for the given token (no-op if already gone).
    /// </summary>
    /// <param name="token">The session token.</param>
    public void Clear(string token) => this.pending.TryRemove(token, out _);

    /// <summary>
    /// Stores all suggestions as a batch and returns an 8-character lowercase hex token.
    /// </summary>
    /// <param name="suggestions">The full list of suggestions to store together.</param>
    /// <returns>The token used to retrieve or clear the batch.</returns>
    public string StoreBatch(IReadOnlyList<AiSuggestion> suggestions)
    {
        var token = GenerateToken();
        this.pendingBatches[token] = suggestions;
        return token;
    }

    /// <summary>
    /// Returns the suggestion batch for the given token, or null if not found.
    /// </summary>
    /// <param name="token">The batch token.</param>
    /// <returns>The suggestions, or null.</returns>
    public IReadOnlyList<AiSuggestion>? GetBatch(string token) => this.pendingBatches.GetValueOrDefault(token);

    /// <summary>
    /// Removes the suggestion batch for the given token (no-op if already gone).
    /// </summary>
    /// <param name="token">The batch token.</param>
    public void ClearBatch(string token) => this.pendingBatches.TryRemove(token, out _);

    private static string GenerateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
