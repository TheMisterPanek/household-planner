// <copyright file="BotActionPayload.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Payload for item-related actions (add, buy, remove).
/// </summary>
public record ItemPayload(string Name, string? Quantity);

/// <summary>
/// Payload for list viewing with pagination metadata.
/// </summary>
public record ListViewedPayload(int Page, int PageSize, int TotalItems);

/// <summary>
/// Payload for actions with no additional data (list viewed, history viewed).
/// </summary>
public record EmptyPayload();

/// <summary>
/// Payload for language preference change.
/// </summary>
public record LanguagePayload(string LanguageCode);

/// <summary>
/// Source-generated JSON serializer context covering all payload types.
/// </summary>
[JsonSerializable(typeof(ItemPayload))]
[JsonSerializable(typeof(EmptyPayload))]
[JsonSerializable(typeof(LanguagePayload))]
[JsonSerializable(typeof(ListViewedPayload))]
public partial class BotActionPayloadContext : JsonSerializerContext
{
}
