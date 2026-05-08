// <copyright file="IPreferenceRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Repositories;

/// <summary>
/// Repository for managing user preferences stored in the UserPreferences table.
/// </summary>
public interface IPreferenceRepository
{
    /// <summary>
    /// Gets the language preference for a chat.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The language code, or null if no preference is set.</returns>
    Task<string?> GetLanguageAsync(long chatId, CancellationToken ct);

    /// <summary>
    /// Saves the language preference for a chat.
    /// </summary>
    /// <param name="chatId">The chat ID.</param>
    /// <param name="languageCode">The language code to save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveLanguageAsync(long chatId, string languageCode, CancellationToken ct);
}
