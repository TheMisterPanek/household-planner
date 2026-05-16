// <copyright file="BuyInputParser.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Text.RegularExpressions;

/// <summary>
/// Parses raw /buy input into a name and optional quantity using a regex-based strategy.
/// </summary>
public static partial class BuyInputParser
{
    [GeneratedRegex(
        @"^(?:купить\s+)?(?<name>.+?)(?:\s+(?<qty>\d+(?:[.,]\d+)?\s*(?:шт(?:уку?|\.)?|кг|г|мл|л|пачк(?:у|и|а)?|банк(?:у|и|а)?|бутылк(?:у|и|а)?|kg|g|ml|l|pcs|(?:piece|pack)s?)?))?$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex QtyPattern();

    /// <summary>
    /// Parses raw text (from /buy command or dialog) into a (Name, Quantity) pair.
    /// Strips a leading "купить " prefix, identifies trailing quantity patterns, and normalizes units.
    /// </summary>
    /// <param name="rawText">The raw user input.</param>
    /// <returns>A tuple of (Name, Quantity); Quantity is null when not detected.</returns>
    public static (string Name, string? Quantity) Parse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return (rawText?.Trim() ?? string.Empty, null);
        }

        var m = QtyPattern().Match(rawText.Trim());
        if (!m.Success)
        {
            return (rawText.Trim(), null);
        }

        var name = m.Groups["name"].Value.Trim();
        var qtyRaw = m.Groups["qty"].Value.Trim();

        if (string.IsNullOrEmpty(qtyRaw))
        {
            // Fallback: if the last token is a standalone integer, treat it as quantity
            var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2 && int.TryParse(tokens[^1], out _))
            {
                return (string.Join(' ', tokens[..^1]).Trim(), tokens[^1]);
            }

            return (name, null);
        }

        return (name, NormalizeQty(qtyRaw));
    }

    private static string NormalizeQty(string qty)
    {
        int i = 0;
        while (i < qty.Length && (char.IsDigit(qty[i]) || qty[i] == '.' || qty[i] == ','))
        {
            i++;
        }

        var numPart = qty[..i].Trim();
        var unitPart = qty[i..].Trim();

        if (string.IsNullOrEmpty(unitPart))
        {
            return numPart;
        }

        return $"{numPart} {NormalizeUnit(unitPart)}";
    }

    private static string NormalizeUnit(string unit)
    {
        var lower = unit.ToLowerInvariant();

        if (lower == "шт" || lower == "шт." || lower.StartsWith("штук") || lower.StartsWith("шту"))
        {
            return "шт";
        }

        if (lower.StartsWith("пачк"))
        {
            return "пачка";
        }

        if (lower.StartsWith("банк"))
        {
            return "банка";
        }

        if (lower.StartsWith("бутылк"))
        {
            return "бутылка";
        }

        if (lower.StartsWith("piece"))
        {
            return "pcs";
        }

        if (lower.StartsWith("pack"))
        {
            return "packs";
        }

        return lower;
    }
}
