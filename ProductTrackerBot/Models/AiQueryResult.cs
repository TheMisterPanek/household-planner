// <copyright file="AiQueryResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// The result of an AI query: cleaned answer text plus optional item suggestions.
/// </summary>
public sealed record AiQueryResult(string Text, IReadOnlyList<AiSuggestion> Suggestions);
