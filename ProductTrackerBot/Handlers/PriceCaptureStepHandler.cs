// <copyright file="PriceCaptureStepHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Processes dialog steps for the optional price-capture dialog after marking an item as bought.
/// Step 1 = store name, Step 2 = price.
/// </summary>
public class PriceCaptureStepHandler : IDialogMessageHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly PendingDialogService<PriceCaptureDialogState> dialogService;
    private readonly PurchaseHistoryRepository purchaseRepository;
    private readonly GroupRepository groupRepository;
    private readonly ILogger<PriceCaptureStepHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriceCaptureStepHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="dialogService">The price-capture dialog state service.</param>
    /// <param name="purchaseRepository">The purchase history repository.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="logger">The logger.</param>
    public PriceCaptureStepHandler(
        ITelegramBotClient botClient,
        PendingDialogService<PriceCaptureDialogState> dialogService,
        PurchaseHistoryRepository purchaseRepository,
        GroupRepository groupRepository,
        ILogger<PriceCaptureStepHandler> logger)
    {
        this.botClient = botClient;
        this.dialogService = dialogService;
        this.purchaseRepository = purchaseRepository;
        this.groupRepository = groupRepository;
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
        }
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

        await this.FinishDialogAsync(message.Chat.Id, message.From!.Id, state, price, cancellationToken);
    }

    private async Task FinishDialogAsync(
        long chatId,
        long userId,
        PriceCaptureDialogState state,
        decimal? price,
        CancellationToken cancellationToken)
    {
        var group = await this.groupRepository.GetOrCreateAsync(chatId);

        var record = new PurchaseRecord
        {
            GroupId = group.Id,
            ItemName = state.ItemName,
            Quantity = state.Quantity,
            StoreName = state.StoreName,
            Price = price,
            PurchasedAt = DateTime.UtcNow,
            BoughtByName = state.BoughtByName,
        };

        await this.purchaseRepository.AddAsync(record);

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

        await this.botClient.SendMessage(
            chatId: chatId,
            text: $"✓ {record.ItemName} recorded{details}",
            cancellationToken: cancellationToken);
    }
}
