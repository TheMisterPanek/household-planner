// <copyright file="LoginCodeStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Collections.Concurrent;
using System.Security.Cryptography;

/// <summary>
/// Thread-safe in-memory store for login codes and session tokens used by the web authentication flow.
/// </summary>
public sealed class LoginCodeStore
{
    private record Entry(string Token, long ChatId, DateTime ExpiresUtc, bool Verified);

    private readonly ConcurrentDictionary<string, Entry> codes = new();
    private readonly ConcurrentDictionary<string, Entry> tokens = new();
    private readonly TimeProvider time;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCodeStore"/> class.
    /// </summary>
    /// <param name="time">Time provider (use <see cref="TimeProvider.System"/> in production).</param>
    public LoginCodeStore(TimeProvider time)
    {
        this.time = time;
    }

    /// <summary>
    /// Generates a one-time 6-digit login code with a 5-minute TTL and returns a 32-byte hex session token.
    /// </summary>
    /// <param name="token">The session token to poll /api/auth/status with.</param>
    /// <returns>A zero-padded 6-digit code string.</returns>
    public string GenerateCode(out string token)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var expiry = this.time.GetUtcNow().UtcDateTime.AddMinutes(5);
        this.codes[code] = new Entry(token, 0, expiry, false);

        var cts = new CancellationTokenSource();
        _ = Task.Delay(TimeSpan.FromMinutes(5), cts.Token)
            .ContinueWith(_ => this.Purge(code), TaskScheduler.Default);

        return code;
    }

    /// <summary>
    /// Verifies a login code submitted via the Telegram bot, moving it from the code store to the token store.
    /// </summary>
    /// <param name="code">The 6-digit code entered by the user.</param>
    /// <param name="chatId">The Telegram chat ID of the verifying user.</param>
    /// <returns><c>true</c> if the code was valid and not yet expired; otherwise <c>false</c>.</returns>
    public bool TryVerify(string code, long chatId)
    {
        if (!this.codes.TryRemove(code, out var entry))
            return false;

        if (this.time.GetUtcNow().UtcDateTime > entry.ExpiresUtc)
            return false;

        var verified = entry with { ChatId = chatId, Verified = true, ExpiresUtc = this.time.GetUtcNow().UtcDateTime.AddHours(24) };
        this.tokens[entry.Token] = verified;
        return true;
    }

    /// <summary>
    /// Retrieves the chat ID associated with a verified session token.
    /// </summary>
    /// <param name="token">The hex session token.</param>
    /// <param name="chatId">The resolved chat ID, or 0 if not found.</param>
    /// <returns><c>true</c> if the token is valid and not expired; otherwise <c>false</c>.</returns>
    public bool TryGetSession(string token, out long chatId)
    {
        chatId = 0;
        if (!this.tokens.TryGetValue(token, out var entry))
            return false;

        if (this.time.GetUtcNow().UtcDateTime > entry.ExpiresUtc)
        {
            this.tokens.TryRemove(token, out _);
            return false;
        }

        chatId = entry.ChatId;
        return true;
    }

    /// <summary>
    /// Removes a pending code from the store (called by the expiry timer or tests).
    /// </summary>
    /// <param name="code">The code to remove.</param>
    public void Purge(string code) => this.codes.TryRemove(code, out _);
}
