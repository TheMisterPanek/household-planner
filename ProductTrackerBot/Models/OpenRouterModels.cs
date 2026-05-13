// <copyright file="OpenRouterModels.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

using System.Text.Json.Serialization;

/// <summary>A single chat message with a role and text content.</summary>
public record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

/// <summary>Request body for the OpenRouter chat completions endpoint.</summary>
public record ChatCompletionRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] ChatMessage[] Messages);

/// <summary>A single choice in the chat completion response.</summary>
public record ChatCompletionChoice(
    [property: JsonPropertyName("message")] ChatMessage Message);

/// <summary>Response from the OpenRouter chat completions endpoint.</summary>
public record ChatCompletionResponse(
    [property: JsonPropertyName("choices")] ChatCompletionChoice[] Choices);

/// <summary>Source-generated JSON serializer context for OpenRouter request/response types.</summary>
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatCompletionResponse))]
public partial class OpenRouterJsonContext : JsonSerializerContext
{
}
