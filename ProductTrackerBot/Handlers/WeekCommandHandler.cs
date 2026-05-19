// <copyright file="WeekCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Collections.Generic;

/// <summary>
/// Handles the /week command — displays the day list for the weekly meal plan.
/// </summary>
public class WeekCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly DayMealsRepository dayMealsRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<WeekCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeekCommandHandler"/> class.
    /// </summary>
    public WeekCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        DayMealsRepository dayMealsRepository,
        ILocalizer localizer,
        ILogger<WeekCommandHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.dayMealsRepository = dayMealsRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string Command => "/week";

    /// <inheritdoc/>
    public string? Description => "View and plan meals for the week";

    /// <inheritdoc/>
    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;

        if (message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
        {
            await this.botClient.SendMessage(
                chatId,
                this.localizer.Get(chatId, "common.group-only"),
                cancellationToken: cancellationToken);
            return;
        }

        var group = await this.groupRepository.GetOrCreateAsync(chatId);
        var monday = WeekViewBuilder.GetWeekMonday(DateOnly.FromDateTime(DateTime.UtcNow));
        var weekStartDate = monday.ToString("yyyy-MM-dd");
        var allPlan = await this.dayMealsRepository.GetWeekAsync(group.Id, weekStartDate);
        var keyboard = WeekViewBuilder.BuildDayListKeyboard(this.localizer, chatId, monday);
        var text = WeekViewBuilder.BuildWeekSummaryText(allPlan, this.localizer, chatId, monday);

        await this.botClient.SendMessage(
            chatId,
            text,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
