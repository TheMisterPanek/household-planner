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
    /// Builds the inline keyboard for the 7-day week view.
    /// </summary>
    /// <param name="plan">Lookup of day-of-week to meal entries.</param>
    /// <param name="hasMeals">Whether the group has any meals in the library.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="chatId">The chat ID for localization.</param>
    /// <returns>An inline keyboard markup.</returns>
    internal static InlineKeyboardMarkup BuildWeekKeyboard(
        ILookup<int, DayMealEntry> plan,
        bool hasMeals,
        ILocalizer localizer,
        long chatId)
    {
        var keyboard = new List<List<InlineKeyboardButton>>();

        for (int day = 1; day <= 7; day++)
        {
            var dayName = localizer.Get(chatId, $"week.day.{day}");
            var dayMeals = plan[day].ToList();

            if (dayMeals.Count > 0)
            {
                foreach (var entry in dayMeals)
                {
                    keyboard.Add(new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData(
                            $"{dayName}: {entry.MealName}",
                            "week:noop"),
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
                            localizer.Get(chatId, "week.btn-set"),
                            $"week:pick:{day}"),
                    });
                }
            }
            else
            {
                var row = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(
                        $"{dayName}: —",
                        "week:noop"),
                };

                if (hasMeals)
                {
                    row.Add(InlineKeyboardButton.WithCallbackData(
                        localizer.Get(chatId, "week.btn-set"),
                        $"week:pick:{day}"));
                }

                keyboard.Add(row);
            }
        }

        return new InlineKeyboardMarkup(keyboard);
    }
}
