// <copyright file="WeekCallbackHandler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using Microsoft.Extensions.Logging;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Handles all week-related callback queries (week:*).
/// </summary>
public class WeekCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly DayMealsRepository dayMealsRepository;
    private readonly MealRepository mealRepository;
    private readonly ILocalizer localizer;
    private readonly ILogger<WeekCallbackHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeekCallbackHandler"/> class.
    /// </summary>
    public WeekCallbackHandler(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        DayMealsRepository dayMealsRepository,
        MealRepository mealRepository,
        ILocalizer localizer,
        ILogger<WeekCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.dayMealsRepository = dayMealsRepository;
        this.mealRepository = mealRepository;
        this.localizer = localizer;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public string CallbackPrefix => "week:";

    /// <inheritdoc/>
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null)
        {
            return;
        }

        var data = callbackQuery.Data;
        var parts = data.Split(':');

        if (parts.Length < 2)
        {
            return;
        }

        var action = parts[1];
        var chatId = callbackQuery.Message!.Chat.Id;
        var group = await this.groupRepository.GetOrCreateAsync(chatId);

        try
        {
            switch (action)
            {
                case "pick":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var pickDay))
                    {
                        await this.HandlePickAsync(callbackQuery, group.Id, chatId, pickDay, cancellationToken);
                    }

                    break;
                case "assign":
                    if (parts.Length >= 4 && int.TryParse(parts[2], out var assignDay) && int.TryParse(parts[3], out var assignMealId))
                    {
                        await this.HandleAssignAsync(callbackQuery, group.Id, chatId, assignDay, assignMealId, cancellationToken);
                    }

                    break;
                case "clear":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var clearDay))
                    {
                        await this.HandleClearAsync(callbackQuery, group.Id, chatId, clearDay, cancellationToken);
                    }

                    break;
                case "back":
                    await this.RenderWeekViewAsync(callbackQuery.Message!, group.Id, chatId, cancellationToken);
                    break;
                case "noop":
                    break;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error handling week callback: {Data}", data);
        }

        await this.botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task HandlePickAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, CancellationToken cancellationToken)
    {
        var meals = await this.mealRepository.GetAllAsync(groupId);

        if (meals.Count == 0)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                this.localizer.Get(chatId, "week.no-meals"),
                cancellationToken: cancellationToken);
            return;
        }

        var keyboard = new List<List<InlineKeyboardButton>>();
        foreach (var meal in meals)
        {
            keyboard.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(meal.Name, $"week:assign:{dayOfWeek}:{meal.Id}"),
            });
        }

        keyboard.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "week.btn-back"), "week:back"),
        });

        var dayName = this.localizer.Get(chatId, $"week.day.{dayOfWeek}");
        var prompt = string.Format(this.localizer.Get(chatId, "week.pick-prompt"), dayName);

        await this.botClient.EditMessageText(
            chatId,
            callbackQuery.Message!.MessageId,
            prompt,
            replyMarkup: new InlineKeyboardMarkup(keyboard),
            cancellationToken: cancellationToken);
    }

    private async Task HandleAssignAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, int mealId, CancellationToken cancellationToken)
    {
        await this.dayMealsRepository.UpsertAsync(groupId, dayOfWeek, mealId);
        await this.RenderWeekViewAsync(callbackQuery.Message!, groupId, chatId, cancellationToken);
    }

    private async Task HandleClearAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, CancellationToken cancellationToken)
    {
        await this.dayMealsRepository.ClearAsync(groupId, dayOfWeek);
        await this.RenderWeekViewAsync(callbackQuery.Message!, groupId, chatId, cancellationToken);
    }

    private async Task RenderWeekViewAsync(Message message, int groupId, long chatId, CancellationToken cancellationToken)
    {
        var plan = await this.dayMealsRepository.GetWeekAsync(groupId);
        var meals = await this.mealRepository.GetAllAsync(groupId);

        var planDict = plan.ToDictionary(e => e.DayOfWeek, e => e);
        var keyboard = WeekViewBuilder.BuildWeekKeyboard(planDict, meals.Count > 0, this.localizer, chatId);

        await this.botClient.EditMessageText(
            chatId,
            message.MessageId,
            this.localizer.Get(chatId, "week.header"),
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
