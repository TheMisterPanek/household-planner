// <copyright file="UpdateHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot;

using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

/// <summary>
/// Handles updates from the Telegram bot API.
/// </summary>
public class UpdateHandler : IUpdateHandler
{
    private readonly ILogger<UpdateHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public UpdateHandler(ILogger<UpdateHandler> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await this.HandleMessageAsync(botClient, update.Message!, cancellationToken);
                    break;

                case UpdateType.EditedMessage:
                case UpdateType.ChannelPost:
                case UpdateType.EditedChannelPost:
                case UpdateType.InlineQuery:
                case UpdateType.ChosenInlineResult:
                case UpdateType.CallbackQuery:
                case UpdateType.ShippingQuery:
                case UpdateType.PreCheckoutQuery:
                case UpdateType.Poll:
                case UpdateType.PollAnswer:
                case UpdateType.MyChatMember:
                case UpdateType.ChatMember:
                case UpdateType.ChatJoinRequest:
                case UpdateType.Unknown:
                default:
                    this.logger.LogDebug("Received unhandled update type: {UpdateType}", update.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    /// <inheritdoc/>
    public async Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        this.logger.LogError(exception, "Error while processing updates from source: {Source}", source);
        await Task.CompletedTask;
    }

    private async Task HandleMessageAsync(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken)
    {
        if (message.Text is null)
        {
            this.logger.LogDebug("Received message without text from user {UserId}", message.From?.Id);
            return;
        }

        this.logger.LogInformation("Received message from {UserId}: {MessageText}", message.From?.Id, message.Text);

        // TODO: Implement echo response
        await Task.CompletedTask;
    }
}
