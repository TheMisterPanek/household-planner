// <copyright file="WeekCommandHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles the /week command — displays and manages the group's weekly meal plan.
/// </summary>
public class WeekCommandHandler : ICommandHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly DayMealsRepository dayMealsRepository;
    private readonly MealRepository mealRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<WeekCommandHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeekCommandHandler"/> class.
    /// </summary>
    public WeekCommandHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        DayMealsRepository dayMealsRepository,
        MealRepository mealRepository,
        ILocalizer localizer,
        ILogger<WeekCommandHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.dayMealsRepository = dayMealsRepository;
        this.mealRepository = mealRepository;
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
        var plan = await this.dayMealsRepository.GetWeekAsync(group.Id);
        var meals = await this.mealRepository.GetAllAsync(group.Id);

        var planDict = plan.ToDictionary(e => e.DayOfWeek, e => e);
        var keyboard = WeekViewBuilder.BuildWeekKeyboard(planDict, meals.Count > 0, this.localizer, chatId);

        await this.botClient.SendMessage(
            chatId,
            this.localizer.Get(chatId, "week.header"),
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
