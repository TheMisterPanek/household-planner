// <copyright file="AiQueryOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot;

/// <summary>
/// Configuration options for the AI natural language query feature.
/// </summary>
public record AiQueryOptions
{
    /// <summary>Gets the OpenRouter API key.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Gets the model used for Round 1 (SQL generation) — benefits from stronger reasoning.</summary>
    public string SqlModel { get; init; } = "openai/gpt-4o-mini";

    /// <summary>Gets the model used for Round 2 (answer formatting) — a free/cheap model is fine.</summary>
    public string AnswerModel { get; init; } = "google/gemini-2.0-flash-exp:free";

    /// <summary>Gets the OpenRouter API base URL.</summary>
    public string BaseUrl { get; init; } = "https://openrouter.ai/api/v1/";

    /// <summary>Gets the file system path to IDENTITY.md.</summary>
    public string IdentityMdPath { get; init; } = string.Empty;
}
