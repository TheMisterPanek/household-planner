// <copyright file="PriceCaptureStepHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Processes dialog steps for the optional price-capture dialog after marking an item as bought.
/// Step 1 = store name, Step 2 = price, Step 3 = expiry date.
/// </summary>
public class PriceCaptureStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<PriceCaptureDialogState> dialogService;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly PriceLogRepository priceLogRepository;
    private readonly GroupRepository groupRepository;
    private readonly TagRepository tagRepository;
    private readonly ExpiryDaySuggestionService suggestionService;
    private readonly ILocalizer localizer;
    private readonly ILogger<PriceCaptureStepHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceCaptureStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The price-capture dialog state service.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="priceLogRepository">The price log repository.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="tagRepository">The tag repository.</param>
    /// <param name="suggestionService">The expiry day suggestion service.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    /// <param name="logger">The logger.</param>
    public PriceCaptureStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<PriceCaptureDialogState> dialogService,
        PurchaseHistoryRepository purchaseRepository,
        PriceLogRepository priceLogRepository,
        GroupRepository groupRepository,
        TagRepository tagRepository,
        ExpiryDaySuggestionService suggestionService,
        ILocalizer localizer,
        ILogger<PriceCaptureStepHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.purchaseRepository = purchaseRepository;
        this.priceLogRepository = priceLogRepository;
        this.groupRepository = groupRepository;
        this.tagRepository = tagRepository;
        this.suggestionService = suggestionService;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(long chatId, long userId)
    {
        var state = this.dialogService.GetState(chatId, userId);
        return state is not null;
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

    /// <summary>
    /// Finishes the dialog by saving the purchase record. Called by step 3 and the expiry-suggest callback.
    /// </summary>
    public async Task FinishDialogAsync(
        long chatId,
        long userId,
        PriceCaptureDialogState state,
        DateOnly? expDate,
        CancellationToken cancellationToken)
    {
        var group = await this.groupRepository.GetOrCreateAsync(chatId);

        var record = new PurchaseRecord
        {
            GroupId = group.Id,
            UserId = userId,
            ItemName = state.ItemName,
            Quantity = state.Quantity,
            StoreName = state.StoreName,
            Price = state.Price,
            PurchasedAt = DateTime.UtcNow,
            BoughtByName = state.BoughtByName,
            ExpDate = expDate,
        };

        await this.purchaseRepository.AddAsync(record);

        if (state.Tags.Count > 0)
        {
            await this.tagRepository.LinkPurchaseHistoryTagsAsync(record.Id, group.Id, state.Tags);
        }

        if (record.Price.HasValue)
        {
            try
            {
                await this.priceLogRepository.AddAsync(
                    group.Id,
                    record.ItemName,
                    record.Price.Value,
                    record.StoreName,
                    DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to log price for item {ItemName}", record.ItemName);
            }
        }

        this.dialogService.ClearState(chatId, userId);

        var details = string.Empty;
        if (record.StoreName is not null)
        {
            details += $" at {record.StoreName}";
        }

        if (record.Price.HasValue)
        {
            details += $" for {record.Price.Value:F2}";
        }

        if (record.ExpDate.HasValue)
        {
            var expirySuffix = this.localizer.Get(chatId, "shop.done-with-expiry")
                .Replace("{expiry}", record.ExpDate.Value.ToString("dd.MM.yyyy"));
            details += expirySuffix;
        }

        await this.botClient.SendMessage(
            chatId: chatId,
            text: $"✓ {record.ItemName} recorded{details}",
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep1Async(Message message, PriceCaptureDialogState state, CancellationToken cancellationToken)
    {
        state.StoreName = message.Text!.Trim();
        state.Step = 2;
        this.dialogService.SetState(message.Chat.Id, message.From!.Id, state);

        var skipPriceButton = InlineKeyboardButton.WithCallbackData("Skip", "price:skip_price");

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: $"💰 Price for {state.ItemName}?",
            replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipPriceButton } }),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep2Async(Message message, PriceCaptureDialogState state, CancellationToken cancellationToken)
    {
        if (!decimal.TryParse(message.Text!.Trim(), out var price))
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "That doesn't look like a price — try again or tap [Skip]",
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
            return;
        }

        state.Price = price;
        state.Step = 3;
        this.dialogService.SetState(message.Chat.Id, message.From!.Id, state);

        var expiryPrompt = this.localizer.Get(message.Chat.Id, "shop.expiry-prompt")
            .Replace("{item}", state.ItemName);

        var suggestions = await this.suggestionService.GetSuggestionsAsync(state.GroupId, state.ItemName);
        var buttons = suggestions
            .Select(d => InlineKeyboardButton.WithCallbackData(
                this.localizer.Get(message.Chat.Id, "expiry.suggest-days").Replace("{n}", d.ToString()),
                $"expiry:suggest:{d}"))
            .ToList();
        buttons.Add(InlineKeyboardButton.WithCallbackData(
            this.localizer.Get(message.Chat.Id, "shop.skip"),
            "price:skip_expiry"));

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: expiryPrompt,
            replyMarkup: new InlineKeyboardMarkup(new[] { buttons.ToArray() }),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep3Async(Message message, PriceCaptureDialogState state, CancellationToken cancellationToken)
    {
        var input = message.Text!.Trim();
        var expDate = ExpiryInputParser.Parse(input);

        if (expDate is null)
        {
            await this.botClient.SendMessage(
                chatId: message.Chat.Id,
                text: this.localizer.Get(message.Chat.Id, "shop.invalid-date"),
                replyParameters: new ReplyParameters { MessageId = message.MessageId },
                cancellationToken: cancellationToken);
            return;
        }

        await this.FinishDialogAsync(message.Chat.Id, message.From!.Id, state, expDate, cancellationToken);
    }
}
