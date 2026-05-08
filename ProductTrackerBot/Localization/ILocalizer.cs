// <copyright file="ILocalizer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Localization;

/// <summary>
/// Provides localized strings by key and chat ID.
/// </summary>
public interface ILocalizer
{
    /// <summary>
    /// Gets a localized string for the given chat ID and key.
    /// </summary>
    /// <param name="chatId">The Telegram chat ID.</param>
    /// <param name="key">The message key (e.g., "buy.group-only").</param>
    /// <returns>The localized string, or the key name if not found (with a warning logged).</returns>
    string Get(long chatId, string key);
}
