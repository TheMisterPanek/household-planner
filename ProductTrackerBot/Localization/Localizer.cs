// <copyright file="Localizer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Localization;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Repositories;

/// <summary>
/// Loads translation files and resolves localized strings by chat language.
/// </summary>
public class Localizer : ILocalizer
{
    private readonly Dictionary<string, Dictionary<string, string>> translations = new();
    private readonly GroupRepository groupRepository;
    private readonly ILogger<Localizer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Localizer"/> class.
    /// </summary>
    /// <param name="groupRepository">The group repository for looking up chat language.</param>
    /// <param name="logger">The logger.</param>
    public Localizer(GroupRepository groupRepository, ILogger<Localizer> logger)
    {
        this.groupRepository = groupRepository;
        this.logger = logger;
        this.LoadTranslations();
    }

    /// <inheritdoc/>
    public string Get(long chatId, string key)
    {
        // Get the language for this chat
        var languageTask = this.groupRepository.GetOrCreateAsync(chatId);
        languageTask.Wait();
        var group = languageTask.Result;
        var languageCode = group.LanguageCode ?? "en";

        // Try to find the translation in the chat's language
        if (this.translations.TryGetValue(languageCode, out var langDict))
        {
            if (langDict.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // Fall back to English
        if (this.translations.TryGetValue("en", out var enDict))
        {
            if (enDict.TryGetValue(key, out var value))
            {
                this.logger.LogWarning("Missing translation for key '{key}' in language '{language}', falling back to English", key, languageCode);
                return value;
            }
        }

        // If still not found, return the key name and log warning
        this.logger.LogWarning("Missing translation for key '{key}' in all languages", key);
        return key;
    }

    private void LoadTranslations()
    {
        var assembly = typeof(Localizer).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        var context = new StringsJsonSerializerContext();
        var options = context.GetTypeInfo(typeof(Dictionary<string, string>)).Options;

        foreach (var resourceName in resourceNames)
        {
            if (!resourceName.StartsWith("ProductTrackerBot.Resources.Strings.") || !resourceName.EndsWith(".json"))
            {
                continue;
            }

            // Extract language code from resource name (e.g., "ProductTrackerBot.Resources.Strings.en.json" -> "en")
            var parts = resourceName.Split('.');
            if (parts.Length < 2)
            {
                continue;
            }

            var languageCode = parts[^2]; // Second to last part before .json

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                this.logger.LogWarning("Could not load embedded resource '{resourceName}'", resourceName);
                continue;
            }

            try
            {
                var typeInfo = (System.Text.Json.JsonTypeInfo<Dictionary<string, string>>)context.GetTypeInfo(typeof(Dictionary<string, string>));
                var translations = JsonSerializer.Deserialize(stream, typeInfo);
                if (translations != null)
                {
                    this.translations[languageCode] = translations;
                    this.logger.LogInformation("Loaded translations for language '{language}'", languageCode);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load translations from resource '{resourceName}'", resourceName);
            }
        }
    }
}
