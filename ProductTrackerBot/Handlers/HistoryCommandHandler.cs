// <copyright file="HistoryCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using System.Text.Json;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the /history command — prints the last 10 actions for the current group chat.
/// </summary>
public class HistoryCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly IHistoryRepository historyRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="historyRepository">The history repository.</param>
    public HistoryCommandHandler(ITelegramBotClient botClient, IHistoryRepository historyRepository)
    {
        this.botClient = botClient;
        this.historyRepository = historyRepository;
    }

    /// <inheritdoc/>
    public string Command => "/history";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Chat.Id == message.From?.Id)
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Эта команда работает только в групповом чате.",
                cancellationToken: cancellationToken);
            return;
        }

        var entries = await this.historyRepository.GetRecentAsync(message.Chat.Id, limit: 10, ct: cancellationToken);

        if (entries.Count == 0)
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "История пуста.",
                cancellationToken: cancellationToken);
            return;
        }

        var lines = entries.Select(e =>
            $"{e.RecordedAt:HH:mm dd.MM} — {e.UserName}: {FormatAction(e)}");

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: string.Join('\n', lines),
            cancellationToken: cancellationToken);

        await this.historyRepository.RecordAsync(
            chatId: message.Chat.Id,
            userId: message.From?.Id ?? 0,
            userName: message.From?.FirstName ?? message.From?.Username ?? "Неизвестный",
            actionType: BotActionType.HistoryViewed,
            payloadJson: "{}",
            ct: cancellationToken);
    }

    private static string FormatAction(BotActionEntry entry)
    {
        switch (entry.ActionType)
        {
            case BotActionType.ItemAdded:
            case BotActionType.ItemBought:
            case BotActionType.ItemRemoved:
                var verb = entry.ActionType switch
                {
                    BotActionType.ItemAdded => "добавил(а)",
                    BotActionType.ItemBought => "купил(а)",
                    _ => "убрал(а)",
                };
                try
                {
                    var payload = JsonSerializer.Deserialize(entry.Payload, BotActionPayloadContext.Default.ItemPayload);
                    if (payload is not null)
                    {
                        return payload.Quantity is not null
                            ? $"{verb} {payload.Name} {payload.Quantity}"
                            : $"{verb} {payload.Name}";
                    }
                }
                catch
                {
                }

                return verb;

            case BotActionType.ListViewed:
                return "просмотрел(а) список";

            case BotActionType.HistoryViewed:
                return "просмотрел(а) историю";

            default:
                return entry.ActionType.ToString();
        }
    }
}
