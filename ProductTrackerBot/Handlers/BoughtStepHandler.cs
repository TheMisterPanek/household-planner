// <copyright file="BoughtStepHandler.cs" company="PlaceholderCompany">
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
/// Processes dialog steps for the /bought command.
/// Step 1 = item name + optional quantity; Step 2 = expiry date.
/// </summary>
public class BoughtStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<BoughtDialogState> dialogService;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<BoughtStepHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoughtStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The bought dialog state service.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="logger">The logger.</param>
    public BoughtStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<BoughtDialogState> dialogService,
        PurchaseHistoryRepository purchaseRepository,
        IHistoryRepository historyRepository,
        ILocalizer localizer,
        ILogger<BoughtStepHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.purchaseRepository = purchaseRepository;
        this.historyRepository = historyRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(long chatId, long userId) =>
        this.dialogService.GetState(chatId, userId) is not null;

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
        }
    }

    /// <summary>
    /// Finishes the dialog by saving the purchase record. Used by both step 2 and the skip-expiry callback.
    /// </summary>
    public async Task FinishDialogAsync(
        long chatId,
        long userId,
        BoughtDialogState state,
        DateOnly? expDate,
        CancellationToken cancellationToken)
    {
        var record = new PurchaseRecord
        {
            GroupId = state.GroupId,
            UserId = userId,
            ItemName = state.ItemName,
            Quantity = state.Quantity,
            StoreName = null,
            Price = null,
            PurchasedAt = DateTime.UtcNow,
            BoughtByName = state.BoughtByName,
            ExpDate = expDate,
        };

        await this.purchaseRepository.AddAsync(record);

        try
        {
            var payload = new ItemPayload(record.ItemName, record.Quantity);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.ItemPayload);
            await this.historyRepository.RecordAsync(
                chatId: chatId,
                userId: userId,
                userName: state.BoughtByName,
                actionType: BotActionType.ItemBought,
                payloadJson: payloadJson,
                revertPayloadJson: null,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to record history for /bought item {ItemName}", record.ItemName);
        }

        this.dialogService.ClearState(chatId, userId);

        string confirmText;
        if (expDate.HasValue)
        {
            confirmText = this.localizer.Get(chatId, "bought.done-with-expiry")
                .Replace("{item}", record.ItemName)
                .Replace("{expiry}", expDate.Value.ToString("dd.MM.yyyy"));
        }
        else
        {
            confirmText = this.localizer.Get(chatId, "bought.done")
                .Replace("{item}", record.ItemName);
        }

        await this.botClient.SendMessage(
            chatId: chatId,
            text: confirmText,
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep1Async(Message message, BoughtDialogState state, CancellationToken cancellationToken)
    {
        var input = message.Text!.Trim();
        var spaceIdx = input.IndexOf(' ');
        string itemName;
        string? quantity;

        if (spaceIdx < 0)
        {
            itemName = input;
            quantity = null;
        }
        else
        {
            itemName = input[..spaceIdx].Trim();
            var qty = input[(spaceIdx + 1)..].Trim();
            quantity = string.IsNullOrEmpty(qty) ? null : qty;
        }

        state.ItemName = itemName;
        state.Quantity = quantity;
        state.Step = 2;
        this.dialogService.SetState(message.Chat.Id, message.From!.Id, state);

        var expiryPrompt = this.localizer.Get(message.Chat.Id, "bought.expiry-prompt")
            .Replace("{item}", itemName);
        var skipButton = InlineKeyboardButton.WithCallbackData(
            this.localizer.Get(message.Chat.Id, "bought.skip-expiry"),
            "bought:skip_expiry");

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: expiryPrompt,
            replyMarkup: new InlineKeyboardMarkup(new[] { new[] { skipButton } }),
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleStep2Async(Message message, BoughtDialogState state, CancellationToken cancellationToken)
    {
        var expDate = ExpiryInputParser.Parse(message.Text!.Trim());

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
