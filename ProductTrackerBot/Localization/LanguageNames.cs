// <copyright file="LanguageNames.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Localization;

public static class LanguageNames
{
    private static readonly Dictionary<string, string> Names = new()
    {
        { "en", "English" },
        { "ru", "Russian" },
        { "pl", "Polish" },
    };

    public static string GetDisplayName(string? languageCode)
        => Names.TryGetValue(languageCode ?? string.Empty, out var name)
            ? name
            : "English";
}
