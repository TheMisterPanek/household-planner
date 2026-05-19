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
    private readonly MealIngredientRepository mealIngredientRepository;
    private readonly ShoppingItemRepository shoppingItemRepository;
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
        MealIngredientRepository mealIngredientRepository,
        ShoppingItemRepository shoppingItemRepository,
        ILocalizer localizer,
        ILogger<WeekCallbackHandler> logger)
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.dayMealsRepository = dayMealsRepository;
        this.mealRepository = mealRepository;
        this.mealIngredientRepository = mealIngredientRepository;
        this.shoppingItemRepository = shoppingItemRepository;
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
                case "nav":
                    // week:nav:{weekStart}
                    if (parts.Length >= 3 && DateOnly.TryParseExact(parts[2], "yyyy-MM-dd", out var navMonday))
                    {
                        await this.RenderDayListViewAsync(callbackQuery.Message!, chatId, navMonday, cancellationToken);
                    }

                    break;
                case "day":
                    // week:day:{weekStart}:{day}
                    if (parts.Length >= 4 && int.TryParse(parts[3], out var dayNum))
                    {
                        await this.RenderDayDetailViewAsync(callbackQuery.Message!, group.Id, chatId, dayNum, parts[2], cancellationToken);
                    }

                    break;
                case "pick":
                    // week:pick:{weekStart}:{day}
                    if (parts.Length >= 4 && int.TryParse(parts[3], out var pickDay))
                    {
                        await this.HandlePickAsync(callbackQuery, group.Id, chatId, pickDay, parts[2], cancellationToken);
                    }

                    break;
                case "assign":
                    // week:assign:{weekStart}:{day}:{mealId}
                    if (parts.Length >= 5 && int.TryParse(parts[3], out var assignDay) && int.TryParse(parts[4], out var assignMealId))
                    {
                        await this.HandleAssignAsync(callbackQuery, group.Id, chatId, assignDay, assignMealId, parts[2], cancellationToken);
                    }

                    break;
                case "clear":
                    // week:clear:{weekStart}:{day}:{mealId}
                    if (parts.Length >= 5 && int.TryParse(parts[3], out var clearDay) && int.TryParse(parts[4], out var clearMealId))
                    {
                        await this.HandleClearMealAsync(callbackQuery, group.Id, chatId, clearDay, clearMealId, parts[2], cancellationToken);
                    }

                    break;
                case "to_cart":
                    // week:to_cart:{weekStart}:{day}
                    if (parts.Length >= 4 && int.TryParse(parts[3], out var cartDay))
                    {
                        await this.HandleAddToCartAsync(callbackQuery, group.Id, chatId, cartDay, parts[2], cancellationToken);
                    }

                    break;
                case "back":
                    // week:back:{weekStart}
                    if (parts.Length >= 3 && DateOnly.TryParseExact(parts[2], "yyyy-MM-dd", out var backMonday))
                    {
                        await this.RenderDayListViewAsync(callbackQuery.Message!, chatId, backMonday, cancellationToken);
                    }

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

    private async Task HandlePickAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, string weekStartDate, CancellationToken cancellationToken)
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
                InlineKeyboardButton.WithCallbackData(meal.Name, $"week:assign:{weekStartDate}:{dayOfWeek}:{meal.Id}"),
            });
        }

        keyboard.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "week.btn-back"), $"week:day:{weekStartDate}:{dayOfWeek}"),
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

    private async Task HandleAssignAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, int mealId, string weekStartDate, CancellationToken cancellationToken)
    {
        var count = await this.dayMealsRepository.GetCountAsync(groupId, dayOfWeek, weekStartDate);
        if (count >= WeekViewBuilder.MaxMealsPerDay)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                this.localizer.Get(chatId, "week.max-meals"),
                cancellationToken: cancellationToken);
            return;
        }

        await this.dayMealsRepository.InsertAsync(groupId, dayOfWeek, mealId, weekStartDate);
        await this.RenderDayDetailViewAsync(callbackQuery.Message!, groupId, chatId, dayOfWeek, weekStartDate, cancellationToken);
    }

    private async Task HandleClearMealAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, int mealId, string weekStartDate, CancellationToken cancellationToken)
    {
        await this.dayMealsRepository.ClearMealAsync(groupId, dayOfWeek, mealId, weekStartDate);
        await this.RenderDayDetailViewAsync(callbackQuery.Message!, groupId, chatId, dayOfWeek, weekStartDate, cancellationToken);
    }

    private async Task HandleAddToCartAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, string weekStartDate, CancellationToken cancellationToken)
    {
        var dayMeals = await this.dayMealsRepository.GetWeekAsync(groupId, weekStartDate);
        var mealsForDay = dayMeals.Where(e => e.DayOfWeek == dayOfWeek).ToList();

        if (mealsForDay.Count == 0)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                this.localizer.Get(chatId, "week.cart-no-meals"),
                cancellationToken: cancellationToken);
            return;
        }

        var existingItems = await this.shoppingItemRepository.GetAllAsync(groupId);
        var existingNames = new HashSet<string>(existingItems.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);

        var addedCount = 0;
        foreach (var dayMeal in mealsForDay)
        {
            var ingredients = await this.mealIngredientRepository.GetAllAsync(dayMeal.MealId);
            foreach (var ingredient in ingredients)
            {
                if (!existingNames.Contains(ingredient.Name))
                {
                    await this.shoppingItemRepository.AddAsync(groupId, ingredient.Name, ingredient.Quantity, "Week");
                    existingNames.Add(ingredient.Name);
                    addedCount++;
                }
            }
        }

        var message = addedCount > 0
            ? string.Format(this.localizer.Get(chatId, "week.cart-added"), addedCount)
            : this.localizer.Get(chatId, "week.cart-no-ingredients");

        await this.botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            message,
            cancellationToken: cancellationToken);
    }

    private async Task RenderDayListViewAsync(Message message, long chatId, DateOnly monday, CancellationToken cancellationToken)
    {
        var group = await this.groupRepository.GetOrCreateAsync(chatId);
        var weekStartDate = monday.ToString("yyyy-MM-dd");
        var allPlan = await this.dayMealsRepository.GetWeekAsync(group.Id, weekStartDate);
        var keyboard = WeekViewBuilder.BuildDayListKeyboard(this.localizer, chatId, monday);
        var text = WeekViewBuilder.BuildWeekSummaryText(allPlan, this.localizer, chatId, monday);

        await this.botClient.EditMessageText(
            chatId,
            message.MessageId,
            text,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task RenderDayDetailViewAsync(Message message, int groupId, long chatId, int dayOfWeek, string weekStartDate, CancellationToken cancellationToken)
    {
        var allPlan = await this.dayMealsRepository.GetWeekAsync(groupId, weekStartDate);
        var dayMeals = allPlan.Where(e => e.DayOfWeek == dayOfWeek).ToList();
        var meals = await this.mealRepository.GetAllAsync(groupId);

        var monday = DateOnly.ParseExact(weekStartDate, "yyyy-MM-dd");
        var dayName = this.localizer.Get(chatId, $"week.day.{dayOfWeek}");
        var dayDate = WeekViewBuilder.FormatDayDate(monday, dayOfWeek, this.localizer, chatId);
        var headerFormat = this.localizer.Get(chatId, "week.day-header");
        var sb = new System.Text.StringBuilder(string.Format(headerFormat, dayName, dayDate));

        if (dayMeals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            foreach (var entry in dayMeals)
            {
                sb.AppendLine($"• {entry.MealName}");
            }
        }

        var keyboard = WeekViewBuilder.BuildDayDetailKeyboard(dayOfWeek, dayMeals, meals.Count > 0, this.localizer, chatId, weekStartDate);

        await this.botClient.EditMessageText(
            chatId,
            message.MessageId,
            sb.ToString(),
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
