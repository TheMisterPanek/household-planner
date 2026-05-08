// <copyright file="LanguageCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles the language selection callback — updates the group's language preference and sends a confirmation.
/// </summary>
public class LanguageCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public LanguageCallbackHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        ILocalizer localizer)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "lang:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || !callbackQuery.Data.StartsWith(this.CallbackPrefix))
        {
            return;
        }

        var languageCode = callbackQuery.Data[this.CallbackPrefix.Length..];
        var chatId = callbackQuery.Message!.Chat.Id;

        await this.groupRepository.SetLanguageAsync(chatId, languageCode);

        var languageDisplayName = languageCode switch
        {
            "en" => "English",
            "ru" => "Русский",
            _ => languageCode,
        };

        var confirmationText = this.localizer.Get(chatId, "language.confirmation")
            .Replace("{language}", languageDisplayName);

        await this.botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: confirmationText,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message != null)
        {
            await this.botClient.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: this.localizer.Get(chatId, "language.select-prompt"),
                cancellationToken: cancellationToken);
        }
    }
}
