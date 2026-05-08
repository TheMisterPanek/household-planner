// <copyright file="Localizer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Localization;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Repositories;

/// <summary>
/// Resolves localized strings from embedded JSON files based on user language preference.
/// </summary>
public class Localizer : ILocalizer
{
    private readonly IPreferenceRepository preferenceRepository;
    private readonly ILogger<Localizer> logger;
    private readonly Dictionary<string, Dictionary<string, string>> translations = new();
    private readonly Dictionary<long, string> languageCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Localizer"/> class.
    /// </summary>
    /// <param name="preferenceRepository">The preference repository for language lookup.</param>
    /// <param name="logger">The logger.</param>
    public Localizer(IPreferenceRepository preferenceRepository, ILogger<Localizer> logger)
    {
        this.preferenceRepository = preferenceRepository;
        this.logger = logger;
        this.LoadTranslations();
    }

    /// <inheritdoc/>
    public async Task<string> GetAsync(long chatId, string key, CancellationToken ct)
    {
        if (!this.languageCache.TryGetValue(chatId, out var language))
        {
            language = await this.preferenceRepository.GetLanguageAsync(chatId, ct) ?? SupportedLanguages.DefaultLanguage;
            this.languageCache[chatId] = language;
        }

        if (!this.translations.TryGetValue(language, out var languageDict))
        {
            language = SupportedLanguages.DefaultLanguage;
            if (!this.translations.TryGetValue(language, out languageDict))
            {
                this.logger.LogWarning("No translations loaded for default language {Language}", SupportedLanguages.DefaultLanguage);
                return key;
            }
        }

        if (languageDict.TryGetValue(key, out var value))
        {
            return value;
        }

        this.logger.LogWarning("Missing localization key {Key} for language {Language}", key, language);
        if (this.translations[SupportedLanguages.DefaultLanguage].TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }

    private void LoadTranslations()
    {
        foreach (var languageCode in SupportedLanguages.Languages.Keys)
        {
            this.LoadLanguageFile(languageCode);
        }
    }

    private void LoadLanguageFile(string language)
    {
        try
        {
            var resourceName = $"ProductTrackerBot.Localization.Strings.{language}.json";
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                this.logger.LogWarning("Could not find embedded resource {ResourceName}", resourceName);
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
            this.translations[language] = dict;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to load translations for language {Language}", language);
        }
    }
}
