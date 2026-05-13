// <copyright file="IOpenRouterClient.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

/// <summary>
/// Sends chat completion requests to the OpenRouter API.
/// </summary>
public interface IOpenRouterClient
{
    /// <summary>
    /// Sends a chat completion request with a system prompt and user message.
    /// </summary>
    /// <param name="model">The model identifier to use (e.g. "openai/gpt-4o-mini").</param>
    /// <param name="systemPrompt">The system prompt.</param>
    /// <param name="userMessage">The user message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assistant's reply content, or <c>null</c> if the response was empty.</returns>
    Task<string?> CompleteAsync(string model, string systemPrompt, string userMessage, CancellationToken ct);
}
