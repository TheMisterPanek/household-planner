// <copyright file="LanguageSelectionHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles language selection callback from settings menu.
/// </summary>
public class LanguageSelectionHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly IPreferenceRepository preferenceRepository;
    private readonly IHistoryRepository historyRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<LanguageSelectionHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageSelectionHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="preferenceRepository">The preference repository.</param>
    /// <param name="historyRepository">The history repository.</param>
    /// <param name="localizer">The localizer for resolved strings.</param>
    /// <param name="logger">The logger.</param>
    public LanguageSelectionHandler(
        ITelegramBotClient botClient,
        IPreferenceRepository preferenceRepository,
        IHistoryRepository historyRepository,
        ILocalizer localizer,
        ILogger<LanguageSelectionHandler> logger)
    {
        this.botClient = botClient;
        this.preferenceRepository = preferenceRepository;
        this.historyRepository = historyRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "settings_lang";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message?.Chat.Id ?? 0;
        var userId = callbackQuery.From?.Id ?? 0;
        var callbackData = callbackQuery.Data ?? string.Empty;

        if (chatId == 0 || userId == 0)
        {
            this.logger.LogWarning("Invalid callback query: missing chat or user ID");
            return;
        }

        var languageCode = this.ExtractLanguageCode(callbackData);
        if (string.IsNullOrEmpty(languageCode))
        {
            this.logger.LogWarning("Invalid callback data format: {CallbackData}", callbackData);
            return;
        }

        try
        {
            await this.preferenceRepository.SaveLanguageAsync(chatId, languageCode, cancellationToken);

            var confirmationMessage = await this.localizer.GetAsync(chatId, "language_saved", cancellationToken);

            await this.botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: confirmationMessage,
                cancellationToken: cancellationToken);

            var payload = new LanguagePayload(languageCode);
            var payloadJson = JsonSerializer.Serialize(payload, BotActionPayloadContext.Default.LanguagePayload);
            await this.historyRepository.RecordAsync(
                chatId: chatId,
                userId: userId,
                userName: callbackQuery.From?.FirstName ?? callbackQuery.From?.Username ?? "Неизвестный",
                actionType: BotActionType.LanguageChanged,
                payloadJson: payloadJson,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to handle language selection for chat {ChatId}, language {Language}", chatId, languageCode);
        }
    }

    private string? ExtractLanguageCode(string callbackData)
    {
        var parts = callbackData.Split(':');
        if (parts.Length >= 2 && parts[0] == this.CallbackPrefix)
        {
            return parts[1];
        }

        return null;
    }
}
