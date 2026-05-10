// <copyright file="LanguageCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /language command — sends an inline keyboard to select the chat's language.
/// </summary>
public class LanguageCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ILocalizer localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="localizer">The localizer for retrieving localized messages.</param>
    public LanguageCommandHandler(ITelegramBotClient botClient, ILocalizer localizer)
    {
        this.botClient = botClient;
        this.localizer = localizer;
    }

    /// <inheritdoc/>
    public string Command => "/language";

    /// <inheritdoc/>
    public string? Description => "Change bot language";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var buttons = new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    text: this.localizer.Get(message.Chat.Id, "language.english"),
                    callbackData: "lang:en"),
                InlineKeyboardButton.WithCallbackData(
                    text: this.localizer.Get(message.Chat.Id, "language.russian"),
                    callbackData: "lang:ru"),
            },
        };

        var keyboard = new InlineKeyboardMarkup(buttons);

        await this.botClient.SendMessage(
            chatId: message.Chat.Id,
            text: this.localizer.Get(message.Chat.Id, "language.select-prompt"),
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
