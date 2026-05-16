// <copyright file="PendingAddService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using ProductTrackerBot.Models;

/// <summary>
/// Holds <see cref="PendingAddItem"/> records keyed by a short random token until confirmed or cancelled.
/// </summary>
public sealed class PendingAddService
{
    private readonly ConcurrentDictionary<string, PendingAddItem> pending = new();

    /// <summary>
    /// Stores a pending item and returns an 8-character hex token.
    /// </summary>
    /// <param name="item">The pending item to store.</param>
    /// <returns>The token used to retrieve or clear the item.</returns>
    public string Store(PendingAddItem item)
    {
        var token = GenerateToken();
        this.pending[token] = item;
        return token;
    }

    /// <summary>
    /// Returns the pending item for the given token, or null if not found.
    /// </summary>
    /// <param name="token">The session token.</param>
    /// <returns>The item, or null.</returns>
    public PendingAddItem? Get(string token) => this.pending.GetValueOrDefault(token);

    /// <summary>
    /// Removes the pending item for the given token (no-op if already gone).
    /// </summary>
    /// <param name="token">The session token.</param>
    public void Clear(string token) => this.pending.TryRemove(token, out _);

    private static string GenerateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
