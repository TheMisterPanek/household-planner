// <copyright file="BuyStepHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Processes dialog steps for the /buy command — step 1 (item name) and step 2 (quantity).
/// </summary>
public class BuyStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<BuyDialogState> dialogService;
    private readonly ShoppingItemRepository itemRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<BuyStepHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuyStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The dialog state service.</param>
    /// <param name="itemRepository">The shopping item repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public BuyStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<BuyDialogState> dialogService,
        ShoppingItemRepository itemRepository,
        IHistoryRepository historyRepository,
        ILocalizer localizer,
        ILogger<BuyStepHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.itemRepository = itemRepository;
        this.historyRepository = historyRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(long chatId, long userId)
    {
        return this.dialogService.GetState(chatId, userId) is not null;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null || message.Text is null)
        {
            return;
        }

        var state = this.dialogService.GetState(message.Chat.Id, message.From.Id);
        if (state is null)
        {
            return;
        }

        var chatId = message.Chat.Id;
        var userId = message.From.Id;

        switch (state.Step)
        {
            case 1:
                await this.HandleStep1Async(message, state, cancellationToken);
                break;
            case 2:
                await this.HandleStep2Async(message, state, cancellationToken);
                break;
            case 3:
                await this.HandleStep3Async(message, state, cancellationToken);
                break;
        }
    }

    private async Task HandleStep1Async(Message message, BuyDialogState state, CancellationToken cancellationToken)
    {
        state.Name = message.Text!.Trim();
        state.Step = 2;
        this.dialogService.SetState(message.Chat.Id, message.From!.Id, state);

        var skipButton = InlineKeyboardButton.WithCallbackData(
            this.localizer.Get(message.Chat.Id, "buy.skip"),
            "buy:skip_quantity");

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: this.localizer.Get(message.Chat.Id, "buy.how-much"),
            replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipButton } }),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep2Async(Message message, BuyDialogState state, CancellationToken cancellationToken)
    {
        state.Quantity = message.Text!.Trim();
        state.Step = 3;
        this.dialogService.SetState(message.Chat.Id, message.From!.Id, state);

        var skipButton = InlineKeyboardButton.WithCallbackData(
            this.localizer.Get(message.Chat.Id, "buy.skip"),
            "buy:skip_expiry");

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: this.localizer.Get(message.Chat.Id, "buy.expiry-date"),
            replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipButton } }),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep3Async(Message message, BuyDialogState state, CancellationToken cancellationToken)
    {
        var input = message.Text!.Trim();
        var expDate = this.ParseExpiryInput(input);

        if (expDate is null)
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: this.localizer.Get(message.Chat.Id, "buy.invalid-date"),
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
            return;
        }

        await this.FinishDialogAsync(message.Chat.Id, message.From!.Id, state, expDate, cancellationToken);
    }

    private DateOnly? ParseExpiryInput(string input)
    {
        var normalized = input.Trim().ToLowerInvariant();

        if (int.TryParse(normalized, out var days) && days > 0 && days <= 365)
        {
            return DateOnly.FromDateTime(DateTime.Now).AddDays(days);
        }

        // Parse popular variants
        var parts = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            if (int.TryParse(parts[0], out var count) && count > 0)
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
        }

        return null;
    }

    private async Task FinishDialogAsync(
        long chatId,
        long userId,
        BuyDialogState state,
        DateOnly? expDate,
        CancellationToken cancellationToken)
    {
        var item = await this.itemRepository.AddAsync(
            groupId: state.GroupId,
            name: state.Name!,
            quantity: state.Quantity,
            addedByName: state.AddedByName,
            expDate: expDate);

        this.dialogService.ClearState(chatId, userId);

        var confirmText = item.Quantity is not null
            ? (item.ExpDate is not null
                ? this.localizer.Get(chatId, "buy.item-added-quantity-expiry")
                    .Replace("{name}", state.AddedByName).Replace("{item}", item.Name)
                    .Replace("{quantity}", item.Quantity).Replace("{expiry}", item.ExpDate.Value.ToString("dd.MM.yyyy"))
                : this.localizer.Get(chatId, "buy.item-added-quantity")
                    .Replace("{name}", state.AddedByName).Replace("{item}", item.Name).Replace("{quantity}", item.Quantity))
            : (item.ExpDate is not null
                ? this.localizer.Get(chatId, "buy.item-added-expiry")
                    .Replace("{name}", state.AddedByName).Replace("{item}", item.Name)
                    .Replace("{expiry}", item.ExpDate.Value.ToString("dd.MM.yyyy"))
                : this.localizer.Get(chatId, "buy.item-added")
                    .Replace("{name}", state.AddedByName).Replace("{item}", item.Name));

        await this.botClient.SendMessage(
            chatId: chatId,
            text: confirmText,
            cancellationToken: cancellationToken);

        try
        {
            var payload = new ItemPayload(item.Name, item.Quantity);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            await this.historyRepository.RecordAsync(
                chatId: chatId,
                userId: userId,
                userName: state.AddedByName,
                actionType: BotActionType.ItemAdded,
                payloadJson: payloadJson,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for ItemAdded");
        }
    }
}
