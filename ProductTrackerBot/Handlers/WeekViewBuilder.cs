// <copyright file="WeekViewBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Shared keyboard-building logic for the weekly meal plan view.
/// </summary>
internal static class WeekViewBuilder
{
    /// <summary>
    /// Maximum number of meals that can be assigned to a single day.
    /// </summary>
    internal const int MaxMealsPerDay = 10;

    /// <summary>
    /// Builds the main day-list keyboard (7 day buttons).
    /// </summary>
    /// <param name="localizer">The localizer.</param>
    /// <param name="chatId">The chat ID for localization.</param>
    /// <returns>An inline keyboard markup.</returns>
    internal static InlineKeyboardMarkup BuildDayListKeyboard(ILocalizer localizer, long chatId)
    {
        var keyboard = new List<List<InlineKeyboardButton>>();
        var row = new List<InlineKeyboardButton>();

        for (int day = 1; day <= 7; day++)
        {
            var dayName = localizer.Get(chatId, $"week.day.{day}");
            row.Add(InlineKeyboardButton.WithCallbackData(dayName, $"week:day:{day}"));

            if (row.Count == 3 || day == 7)
            {
                keyboard.Add(row);
                row = new List<InlineKeyboardButton>();
            }
        }

        return new InlineKeyboardMarkup(keyboard);
    }

    /// <summary>
    /// Builds the day detail keyboard (meals for a specific day + action buttons).
    /// </summary>
    /// <param name="day">The day of week (1=Monday, 7=Sunday).</param>
    /// <param name="dayMeals">Meals assigned to this day.</param>
    /// <param name="hasMeals">Whether the group has any meals in the library.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="chatId">The chat ID for localization.</param>
    /// <returns>An inline keyboard markup.</returns>
    internal static InlineKeyboardMarkup BuildDayDetailKeyboard(
        int day,
        IReadOnlyList<DayMealEntry> dayMeals,
        bool hasMeals,
        ILocalizer localizer,
        long chatId)
    {
        var keyboard = new List<List<InlineKeyboardButton>>();

        foreach (var entry in dayMeals)
        {
            keyboard.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(entry.MealName, "week:noop"),
                InlineKeyboardButton.WithCallbackData(
                    localizer.Get(chatId, "week.btn-clear"),
                    $"week:clear:{day}:{entry.MealId}"),
            });
        }

        if (hasMeals && dayMeals.Count < MaxMealsPerDay)
        {
            keyboard.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    localizer.Get(chatId, "week.btn-add-meal"),
                    $"week:pick:{day}"),
            });
        }

        keyboard.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData(
                localizer.Get(chatId, "week.btn-back"),
                "week:back"),
            InlineKeyboardButton.WithCallbackData(
                localizer.Get(chatId, "week.btn-to-cart"),
                $"week:to_cart:{day}"),
        });

        return new InlineKeyboardMarkup(keyboard);
    }
}
