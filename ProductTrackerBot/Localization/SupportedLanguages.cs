// <copyright file="SupportedLanguages.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Localization;

/// <summary>
/// Configuration for supported languages and their localization keys.
/// </summary>
public static class SupportedLanguages
{
    /// <summary>
    /// Default language code used when user has no preference.
    /// </summary>
    public const string DefaultLanguage = "en";

    /// <summary>
    /// List of supported language codes and their display labels.
    /// To add a new language:
    /// 1. Add entry here with code and label key
    /// 2. Create Strings.{code}.json file in Localization directory
    /// 3. Add it to ProjectTrackerBot.csproj as EmbeddedResource
    /// </summary>
    public static readonly Dictionary<string, string> Languages = new()
    {
        { "en", "language_english" },
        { "ru", "language_russian" },
        { "pl", "language_polish" },

        // { "es", "language_spanish" },   // To add Spanish: add here, create Strings.es.json, update csproj
        // { "fr", "language_french" },    // To add French: add here, create Strings.fr.json, update csproj
    };
}
