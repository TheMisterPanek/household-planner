// <copyright file="AiSuggestion.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// A shopping item suggested by the AI.
/// </summary>
public sealed record AiSuggestion(string Name, string? Count);
