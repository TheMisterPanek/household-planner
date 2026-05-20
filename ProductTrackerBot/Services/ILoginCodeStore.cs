// <copyright file="ILoginCodeStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

/// <summary>
/// Manages short-lived login codes and session tokens used for Telegram-based web auth.
/// </summary>
public interface ILoginCodeStore
{
    /// <summary>Generates a one-time login code and an associated session token.</summary>
    /// <param name="token">The session token to poll for auth completion.</param>
    /// <returns>A 6-character uppercase alphanumeric code.</returns>
    string GenerateCode(out string token);

    /// <summary>Returns the number of seconds remaining until the given code expires.</summary>
    /// <param name="code">The code returned by <see cref="GenerateCode"/>.</param>
    /// <returns>Remaining seconds (≥ 0); 0 if the code is unknown or already expired.</returns>
    int GetRemainingSeconds(string code);

    /// <summary>Verifies a login code submitted via the Telegram bot.</summary>
    bool TryVerify(string code, long chatId);

    /// <summary>Retrieves the chat ID for a verified session token.</summary>
    bool TryGetSession(string token, out long chatId);

    /// <summary>Removes a pending code from the store.</summary>
    void Purge(string code);
}
