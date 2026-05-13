// <copyright file="BotActionEntry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// A single row from the <c>BotActionHistory</c> table.
/// </summary>
public record BotActionEntry
{
    /// <summary>Gets the database primary key.</summary>
    public long Id { get; init; }

    /// <summary>Gets the Telegram chat ID.</summary>
    public long ChatId { get; init; }

    /// <summary>Gets the Telegram user ID.</summary>
    public long UserId { get; init; }

    /// <summary>Gets the display name of the user.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the type of action performed.</summary>
    public BotActionType ActionType { get; init; }

    /// <summary>Gets the JSON payload for the action.</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>Gets when the action was recorded (UTC).</summary>
    public DateTime RecordedAt { get; init; }

    /// <summary>Gets the JSON payload used to revert this action, or null if not reversible.</summary>
    public string? RevertPayload { get; init; }

    /// <summary>Gets the UTC timestamp when this entry was reverted, or null if not yet reverted.</summary>
    public string? RevertedAt { get; init; }
}
