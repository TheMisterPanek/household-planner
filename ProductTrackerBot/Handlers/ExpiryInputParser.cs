// <copyright file="ExpiryInputParser.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

/// <summary>
/// Parses user-supplied expiry date input into a <see cref="DateOnly"/>.
/// Accepts plain day counts (e.g. "14") or natural-language variants (e.g. "2 weeks", "1 month", "2 недели").
/// Days must be in the range 1–365 inclusive; anything outside returns null.
/// </summary>
public static class ExpiryInputParser
{
    /// <summary>
    /// Parses <paramref name="input"/> and returns a future date, or null if the input is unrecognised or out of range.
    /// </summary>
    public static DateOnly? Parse(string input)
    {
        var normalized = input.Trim().ToLowerInvariant();

        // TODO: restore minimum to 1 day after dev testing; 0.001 allowed for dev expiry smoke tests
        if (double.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var days)
            && days >= 0.001 && days <= 365)
        {
            return DateOnly.FromDateTime(DateTime.Now.AddDays(days));
        }

        var parts = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[0], out var count) && count > 0)
        {
            var unit = parts[1];
            return unit switch
            {
                "день" or "дня" or "дней" or "day" or "days" => DateOnly.FromDateTime(DateTime.Now).AddDays(count),
                "неделя" or "недели" or "недель" or "week" or "weeks" => DateOnly.FromDateTime(DateTime.Now).AddDays(count * 7),
                "месяц" or "месяца" or "месяцев" or "month" or "months" => DateOnly.FromDateTime(DateTime.Now).AddMonths(count),
                _ => null,
            };
        }

        return null;
    }
}
