// <copyright file="StringsJsonSerializerContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Localization;

using System.Text.Json.Serialization;

/// <summary>
/// Source-generated JSON serializer context for AOT-safe deserialization of translation files.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class StringsJsonSerializerContext : JsonSerializerContext
{
}
