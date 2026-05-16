// <copyright file="PendingEditService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using ProductTrackerBot.Models;

/// <summary>
/// Holds <see cref="PendingEditItem"/> records keyed by a short random token until confirmed or cancelled.
/// </summary>
public sealed class PendingEditService
{
    private readonly ConcurrentDictionary<string, PendingEditItem> pending = new();

    /// <summary>
    /// Stores a pending edit and returns an 8-character hex token.
    /// </summary>
    /// <param name="item">The pending edit item to store.</param>
    /// <returns>The token used to retrieve or clear the item.</returns>
    public string Store(PendingEditItem item)
    {
        var token = GenerateToken();
        this.pending[token] = item;
        return token;
    }

    /// <summary>
    /// Returns the pending edit for the given token, or null if not found.
    /// </summary>
    /// <param name="token">The session token.</param>
    /// <returns>The item, or null.</returns>
    public PendingEditItem? Get(string token) => this.pending.GetValueOrDefault(token);

    /// <summary>
    /// Removes the pending edit for the given token (no-op if already gone).
    /// </summary>
    /// <param name="token">The session token.</param>
    public void Clear(string token) => this.pending.TryRemove(token, out _);

    private static string GenerateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
