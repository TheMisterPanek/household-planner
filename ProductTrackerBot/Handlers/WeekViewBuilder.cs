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
    /// Builds the inline keyboard for the 7-day week view.
    /// </summary>
    /// <param name="plan">Dictionary of day-of-week to meal entry.</param>
    /// <param name="hasMeals">Whether the group has any meals in the library.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="chatId">The chat ID for localization.</param>
    /// <returns>An inline keyboard markup.</returns>
    internal static InlineKeyboardMarkup BuildWeekKeyboard(
        Dictionary<int, DayMealEntry> plan,
        bool hasMeals,
        ILocalizer localizer,
        long chatId)
    {
        var keyboard = new List<List<InlineKeyboardButton>>();

        for (int day = 1; day <= 7; day++)
        {
            var dayName = localizer.Get(chatId, $"week.day.{day}");
            var row = new List<InlineKeyboardButton>();

            if (plan.TryGetValue(day, out var entry))
            {
                row.Add(InlineKeyboardButton.WithCallbackData(
                    $"{dayName}: {entry.MealName}",
                    "week:noop"));
                row.Add(InlineKeyboardButton.WithCallbackData(
                    localizer.Get(chatId, "week.btn-clear"),
                    $"week:clear:{day}"));
            }
            else
            {
                row.Add(InlineKeyboardButton.WithCallbackData(
                    $"{dayName}: —",
                    "week:noop"));

                if (hasMeals)
                {
                    row.Add(InlineKeyboardButton.WithCallbackData(
                        localizer.Get(chatId, "week.btn-set"),
                        $"week:pick:{day}"));
                }
            }

            keyboard.Add(row);
        }

        return new InlineKeyboardMarkup(keyboard);
    }
}
