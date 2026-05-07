// <copyright file="Group.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Models;

/// <summary>
/// Represents a registered Telegram group chat.
/// </summary>
public record Group
{
    /// <summary>
    /// Gets the database primary key.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the Telegram chat ID.
    /// </summary>
    public long ChatId { get; init; }

    /// <summary>
    /// Gets or sets the message ID of the pinned shopping list message.
    /// </summary>
    public int? ListMessageId { get; set; }
}
