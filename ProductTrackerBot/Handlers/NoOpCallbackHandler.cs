// <copyright file="NoOpCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Telegram.Bot;
using Telegram.Bot.Types;

/// <summary>
/// Handles taps on non-interactive spacer buttons — acknowledges the callback and takes no other action.
/// </summary>
public class NoOpCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoOpCallbackHandler"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    public NoOpCallbackHandler(ITelegramBotClient botClient)
    {
        this.botClient = botClient;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "noop";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        await this.botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }
}
