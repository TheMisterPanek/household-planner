// <copyright file="BotActionPayloadContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Source-generated JSON serializer context covering all payload types.
/// </summary>
[JsonSerializable(typeof(ItemPayload))]
[JsonSerializable(typeof(EmptyPayload))]
[JsonSerializable(typeof(LanguagePayload))]
[JsonSerializable(typeof(ListViewedPayload))]
[JsonSerializable(typeof(ItemBoughtRevert))]
[JsonSerializable(typeof(ItemRemovedRevert))]
[JsonSerializable(typeof(ListArchivedRevert))]
public partial class BotActionPayloadContext : JsonSerializerContext
{
}


