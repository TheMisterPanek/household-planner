// <copyright file="ILocalizer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Localization;

/// <summary>
/// Resolves localized strings by key for a given chat.
/// </summary>
public interface ILocalizer
{
    /// <summary>
    /// Gets a localized string for a chat and key.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="key">The localization key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The localized string, or a fallback if not found.</returns>
    Task<string> GetAsync(long chatId, string key, CancellationToken ct);
}
