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
/// Payload for actions with no additional data (list viewed, history viewed).
/// </summary>
public record EmptyPayload();

/// <summary>
/// Source-generated JSON serializer context covering all payload types.
/// </summary>
[JsonSerializable(typeof(ItemPayload))]
[JsonSerializable(typeof(EmptyPayload))]
public partial class BotActionPayloadContext : JsonSerializerContext
{
}
