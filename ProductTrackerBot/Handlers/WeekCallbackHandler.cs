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
                case "day":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var dayNum))
                    {
                        await this.RenderDayDetailViewAsync(callbackQuery.Message!, group.Id, chatId, dayNum, cancellationToken);
                    }

                    break;
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
                    if (parts.Length >= 4 && int.TryParse(parts[2], out var clearDay) && int.TryParse(parts[3], out var clearMealId))
                    {
                        await this.HandleClearMealAsync(callbackQuery, group.Id, chatId, clearDay, clearMealId, cancellationToken);
                    }

                    break;
                case "to_cart":
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var cartDay))
                    {
                        await this.HandleAddToCartAsync(callbackQuery, group.Id, chatId, cartDay, cancellationToken);
                    }

                    break;
                case "back":
                    await this.RenderDayListViewAsync(callbackQuery.Message!, chatId, cancellationToken);
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
            InlineKeyboardButton.WithCallbackData(this.localizer.Get(chatId, "week.btn-back"), $"week:day:{dayOfWeek}"),
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
        var count = await this.dayMealsRepository.GetCountAsync(groupId, dayOfWeek);
        if (count >= WeekViewBuilder.MaxMealsPerDay)
        {
            await this.botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                this.localizer.Get(chatId, "week.max-meals"),
                cancellationToken: cancellationToken);
            return;
        }

        await this.dayMealsRepository.InsertAsync(groupId, dayOfWeek, mealId);
        await this.RenderDayDetailViewAsync(callbackQuery.Message!, groupId, chatId, dayOfWeek, cancellationToken);
    }

    private async Task HandleClearMealAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, int mealId, CancellationToken cancellationToken)
    {
        await this.dayMealsRepository.ClearMealAsync(groupId, dayOfWeek, mealId);
        await this.RenderDayDetailViewAsync(callbackQuery.Message!, groupId, chatId, dayOfWeek, cancellationToken);
    }

    private async Task HandleAddToCartAsync(CallbackQuery callbackQuery, int groupId, long chatId, int dayOfWeek, CancellationToken cancellationToken)
    {
        var dayMeals = await this.dayMealsRepository.GetWeekAsync(groupId);
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

    private async Task RenderDayListViewAsync(Message message, long chatId, CancellationToken cancellationToken)
    {
        var keyboard = WeekViewBuilder.BuildDayListKeyboard(this.localizer, chatId);

        await this.botClient.EditMessageText(
            chatId,
            message.MessageId,
            this.localizer.Get(chatId, "week.header"),
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task RenderDayDetailViewAsync(Message message, int groupId, long chatId, int dayOfWeek, CancellationToken cancellationToken)
    {
        var allPlan = await this.dayMealsRepository.GetWeekAsync(groupId);
        var dayMeals = allPlan.Where(e => e.DayOfWeek == dayOfWeek).ToList();
        var meals = await this.mealRepository.GetAllAsync(groupId);

        var dayName = this.localizer.Get(chatId, $"week.day.{dayOfWeek}");
        var header = $"📅 {dayName}:";

        var keyboard = WeekViewBuilder.BuildDayDetailKeyboard(dayOfWeek, dayMeals, meals.Count > 0, this.localizer, chatId);

        await this.botClient.EditMessageText(
            chatId,
            message.MessageId,
            header,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
}
