// <copyright file="SettingsCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /settings command — displays settings menu with language selection.
/// </summary>
public class SettingsCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly ILocalizer localizer;
    private readonly ILogger<SettingsCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsCommandHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="localizer">The localizer for resolved strings.</param>
    /// <param name="logger">The logger.</param>
    public SettingsCommandHandler(
        ITelegramBotClient botClient,
        ILocalizer localizer,
        ILogger<SettingsCommandHandler> logger)
    {
        this.botClient = botClient;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Command => "/settings";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;

        var menuPrompt = this.localizer.Get(chatId, "menu_prompt");

        var buttons = new List<InlineKeyboardButton[]>();
        foreach (var (languageCode, labelKey) in SupportedLanguages.Languages)
        {
            var label = this.localizer.Get(chatId, labelKey);
            var button = InlineKeyboardButton.WithCallbackData(label, $"settings_lang:{languageCode}");
            buttons.Add(new[] { button });
        }

        var keyboard = new InlineKeyboardMarkup(buttons);

        try
        {
            await this.botClient.SendMessage(
                chatId: chatId,
                text: menuPrompt,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to send settings menu");
        }
    }
}
