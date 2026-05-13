// <copyright file="OpenRouterClient.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Text;
using System.Text.Json;
using ProductTrackerBot.Models;

/// <summary>
/// Sends chat completion requests to OpenRouter via its OpenAI-compatible API.
/// </summary>
public class OpenRouterClient : IOpenRouterClient
{
    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenRouterClient"/> class.
    /// </summary>
    /// <param name="httpClient">Pre-configured HTTP client (base URL and auth set by factory).</param>
    /// <param name="options">AI query configuration (unused directly; model is passed per call).</param>
    public OpenRouterClient(HttpClient httpClient, AiQueryOptions options)
    {
        this.httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<string?> CompleteAsync(string model, string systemPrompt, string userMessage, CancellationToken ct)
    {
        var request = new ChatCompletionRequest(
            model,
            new[]
            {
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", userMessage),
            });

        var json = JsonSerializer.Serialize(request, OpenRouterJsonContext.Default.ChatCompletionRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await this.httpClient.PostAsync("chat/completions", content, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var completion = JsonSerializer.Deserialize(body, OpenRouterJsonContext.Default.ChatCompletionResponse);

        return completion?.Choices?.FirstOrDefault()?.Message?.Content;
    }
}
