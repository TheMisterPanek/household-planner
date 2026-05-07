// <copyright file="BotConfiguration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot;

/// <summary>
/// Configuration for the Telegram bot.
/// </summary>
public record BotConfiguration
{
    /// <summary>
    /// Gets the Telegram bot API token.
    /// </summary>
    public required string Token { get; init; }
}
