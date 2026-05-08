// <copyright file="PricesCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handler for the /prices command to display price statistics for an item.
/// </summary>
public class PricesCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PriceLogRepository priceLogRepository;
    private readonly GroupRepository groupRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricesCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="priceLogRepository">The price log repository.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="localizer">The localizer service.</param>
    public PricesCommandHandler(
        ITelegramBotClient botClient,
        PriceLogRepository priceLogRepository,
        GroupRepository groupRepository,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.priceLogRepository = priceLogRepository;
        this.groupRepository = groupRepository;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string Command => "/prices";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: this.localizer.Get(message.Chat.Id, "prices.group-only"),
                cancellationToken: cancellationToken);
            return;
        }

        var argument = message.Text!.Substring(this.Command.Length).Trim();

        if (string.IsNullOrEmpty(argument))
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: this.localizer.Get(message.Chat.Id, "prices.usage"),
                cancellationToken: cancellationToken);
            return;
        }

        var group = await this.groupRepository.GetOrCreateAsync(message.Chat.Id);
        var stats = await this.priceLogRepository.GetStatsAsync(group.Id, argument);

        if (stats is null)
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: this.localizer.Get(message.Chat.Id, "prices.not-found"),
                cancellationToken: cancellationToken);
            return;
        }

        var reply = this.FormatStatsReply(argument, stats, message.Chat.Id);

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: reply,
            cancellationToken: cancellationToken);
    }

    private string FormatStatsReply(string itemName, Models.PriceStats stats, long chatId)
    {
        var lines = new List<string>
        {
            $"{itemName}:",
            $"Min: {stats.Min:F2}, Avg: {stats.Avg:F2}, Max: {stats.Max:F2}, Count: {stats.Count}",
        };

        if (stats.StoreBreakdown.Count > 0)
        {
            lines.Add(string.Empty);
            foreach (var storeStats in stats.StoreBreakdown)
            {
                var storeName = storeStats.StoreName ?? this.localizer.Get(chatId, "prices.unknown-store");
                lines.Add($"{storeName}: Min: {storeStats.Min:F2}, Avg: {storeStats.Avg:F2}, Max: {storeStats.Max:F2}, Count: {storeStats.Count}");
            }
        }

        return string.Join("\n", lines);
    }
}
