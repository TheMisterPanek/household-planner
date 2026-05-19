// <copyright file="Localizer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Localization;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Repositories;

/// <summary>
/// Loads translation files and resolves localized strings by chat language.
/// </summary>
public class Localizer : ILocalizer
{
    private readonly Dictionary<string, Dictionary<string, string>> translations = new();
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<Localizer> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Localizer"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory for creating short-lived scopes.</param>
    /// <param name="logger">The logger.</param>
    public Localizer(IServiceScopeFactory scopeFactory, ILogger<Localizer> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.LoadTranslations();
    }

    /// <inheritdoc/>
    public string Get(long chatId, string key)
    {
        using var scope = this.scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<GroupRepository>();
        var languageTask = repo.GetOrCreateAsync(chatId);
        languageTask.Wait();
        var group = languageTask.Result;
        var languageCode = group.LanguageCode ?? "en";

        return this.GetTranslation(languageCode, key);
    }

    /// <inheritdoc/>
    public async Task<string> GetAsync(long chatId, string key, CancellationToken cancellationToken)
    {
        using var scope = this.scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<GroupRepository>();
        var group = await repo.GetOrCreateAsync(chatId);
        var languageCode = group.LanguageCode ?? "en";
        return this.GetTranslation(languageCode, key);
    }

    private string GetTranslation(string languageCode, string key)
    {
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
        var context = new StringsJsonSerializerContext();
        var basePath = AppContext.BaseDirectory;
        var localizationPath = Path.Combine(basePath, "Localization");

        if (!Directory.Exists(localizationPath))
        {
            this.logger.LogWarning("Could not find Localization directory at {Path}", localizationPath);
            return;
        }

        var foundPath = localizationPath;

        var files = Directory.GetFiles(foundPath, "Strings.*.json");
        if (files.Length == 0)
        {
            this.logger.LogWarning("No translation files found in {Path}", foundPath);
            return;
        }

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var parts = fileName.Split('.');
            if (parts.Length != 2)
            {
                continue;
            }

            var languageCode = parts[1];

            try
            {
                var json = File.ReadAllText(filePath);
                var typeInfo = context.GetTypeInfo(typeof(Dictionary<string, string>))!;
                var translations = JsonSerializer.Deserialize(json, typeInfo) as Dictionary<string, string>;
                if (translations != null)
                {
                    this.translations[languageCode] = translations;
                    this.logger.LogInformation("Loaded {Count} translations for language '{language}'", translations.Count, languageCode);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load translations from file '{FilePath}'", filePath);
            }
        }
    }
}
