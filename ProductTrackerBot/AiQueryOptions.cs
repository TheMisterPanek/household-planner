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

    /// <summary>Gets the model used for all AI queries.</summary>
    public string Model { get; init; } = "openai/gpt-4o-mini";

    /// <summary>Gets the OpenRouter API base URL.</summary>
    public string BaseUrl { get; init; } = "https://openrouter.ai/api/v1/";

    /// <summary>Gets the file system path to IDENTITY.md.</summary>
    public string IdentityMdPath { get; init; } = string.Empty;
}
