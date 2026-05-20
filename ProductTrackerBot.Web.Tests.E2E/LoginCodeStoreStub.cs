// <copyright file="LoginCodeStoreStub.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Web.Tests.E2E;

using ProductTrackerBot.Services;

/// <summary>
/// Test double for <see cref="ILoginCodeStore"/>.
/// Returns a fixed code and lets tests control the remaining TTL.
/// </summary>
public sealed class LoginCodeStoreStub : ILoginCodeStore
{
    private const string FixedCode = "AABBCC";
    private const string FixedToken = "stub-session-token-aabbcc";

    /// <summary>
    /// Gets or sets the seconds returned by <see cref="GetRemainingSeconds"/>.
    /// Set to 1 to simulate an expired code quickly; default 300 for normal flow.
    /// </summary>
    public int ForcedTtlSeconds { get; set; } = 300;

    /// <inheritdoc/>
    public string GenerateCode(out string token)
    {
        token = FixedToken;
        return FixedCode;
    }

    /// <inheritdoc/>
    public int GetRemainingSeconds(string code) => ForcedTtlSeconds;

    /// <inheritdoc/>
    public bool TryVerify(string code, long chatId) => false;

    /// <inheritdoc/>
    public bool TryGetSession(string token, out long chatId)
    {
        chatId = 0;
        return false;
    }

    /// <inheritdoc/>
    public void Purge(string code) { }
}
