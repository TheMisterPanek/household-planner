// <copyright file="SearchCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using System.Text;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the /search command — looks up purchase history by item name.
/// </summary>
public class SearchCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly GroupRepository groupRepository;
    private readonly ILogger<SearchCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="logger">The logger.</param>
    public SearchCommandHandler(
        ITelegramBotClient botClient,
        PurchaseHistoryRepository purchaseRepository,
        GroupRepository groupRepository,
        ILogger<SearchCommandHandler> logger)
    {
        this.botClient = botClient;
        this.purchaseRepository = purchaseRepository;
        this.groupRepository = groupRepository;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Command => "/search";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var query = ExtractQuery(message.Text);

        if (string.IsNullOrWhiteSpace(query))
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Usage: /search <item name>",
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
            return;
        }

        var group = await this.groupRepository.GetOrCreateAsync(message.Chat.Id);
        var results = await this.purchaseRepository.SearchAsync(group.Id, query);

        if (results.Count == 0)
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"No purchase history found for '{query}'",
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
            return;
        }

        var reply = FormatResults(results, query);
        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: reply,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    internal static string FormatResults(IReadOnlyList<PurchaseRecord> records, string query)
    {
        // Group by item name (case-insensitive)
        var groups = records
            .GroupBy(r => r.ItemName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key);

        var sb = new StringBuilder();
        sb.AppendLine($"📊 Search results for '{query}':");
        sb.AppendLine();

        foreach (var group in groups)
        {
            var ordered = group.OrderByDescending(r => r.PurchasedAt).ToList();
            var latest = ordered[0];
            var latestPrice = latest.Price;

            sb.Append($"• {group.Key}");

            if (latestPrice.HasValue)
            {
                var delta = CalculateDelta(ordered);
                sb.Append($" — {latestPrice.Value:F2}");
                if (delta is not null)
                {
                    sb.Append($" ({delta})");
                }

                if (latest.StoreName is not null)
                {
                    sb.Append($" at {latest.StoreName}");
                }
            }
            else if (latest.StoreName is not null)
            {
                sb.Append($" at {latest.StoreName}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    internal static string? CalculateDelta(List<PurchaseRecord> orderedRecords)
    {
        // Find the two most recent records with non-null price
        var pricedRecords = orderedRecords.Where(r => r.Price.HasValue).ToList();
        if (pricedRecords.Count < 2)
        {
            return null;
        }

        var latestPrice = pricedRecords[0].Price!.Value;
        var previousPrice = pricedRecords[1].Price!.Value;

        if (previousPrice == 0)
        {
            return null;
        }

        var delta = Math.Round((latestPrice - previousPrice) / previousPrice * 100, 1);
        var sign = delta > 0 ? "+" : string.Empty;
        return $"{sign}{delta:F1}%";
    }

    private static string? ExtractQuery(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var spaceIdx = text.IndexOf(' ');
        if (spaceIdx < 0)
        {
            return null;
        }

        return text[(spaceIdx + 1)..].Trim();
    }
}
