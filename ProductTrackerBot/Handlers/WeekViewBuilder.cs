// <copyright file="WeekViewBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Handlers;

using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text;

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
    /// Returns the Monday of the ISO week containing <paramref name="date"/>.
    /// </summary>
    internal static DateOnly GetWeekMonday(DateOnly date)
    {
        int offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }

    /// <summary>
    /// Formats a week range string, e.g. "May 18–24, 2026" or "May 29 – Jun 4, 2026".
    /// </summary>
    internal static string FormatWeekRange(DateOnly monday, ILocalizer localizer, long chatId)
    {
        var sunday = monday.AddDays(6);
        var startMonth = localizer.Get(chatId, $"week.month.{monday.Month}");
        if (monday.Month == sunday.Month)
        {
            return $"{startMonth} {monday.Day}–{sunday.Day}, {monday.Year}";
        }

        var endMonth = localizer.Get(chatId, $"week.month.{sunday.Month}");
        return $"{startMonth} {monday.Day} – {endMonth} {sunday.Day}, {sunday.Year}";
    }

    /// <summary>
    /// Returns the abbreviated date for a day within a week, e.g. "May 18".
    /// </summary>
    internal static string FormatDayDate(DateOnly monday, int dayOfWeek, ILocalizer localizer, long chatId)
    {
        var date = monday.AddDays(dayOfWeek - 1);
        var monthAbbr = localizer.Get(chatId, $"week.month.{date.Month}");
        return $"{monthAbbr} {date.Day}";
    }

    /// <summary>
    /// Builds the week summary message text showing all 7 days with their assigned meals.
    /// </summary>
    /// <param name="allPlan">All day-meal entries for the week.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="chatId">The chat ID for localization.</param>
    /// <param name="monday">The Monday of the week being displayed.</param>
    /// <returns>Formatted summary string.</returns>
    internal static string BuildWeekSummaryText(IReadOnlyList<DayMealEntry> allPlan, ILocalizer localizer, long chatId, DateOnly monday)
    {
        var byDay = allPlan.GroupBy(e => e.DayOfWeek).ToDictionary(g => g.Key, g => g.Select(e => e.MealName).ToList());
        var range = FormatWeekRange(monday, localizer, chatId);
        var sb = new StringBuilder($"{localizer.Get(chatId, "week.header")} {range}");
        sb.AppendLine();
        for (int day = 1; day <= 7; day++)
        {
            var dayName = localizer.Get(chatId, $"week.day.{day}");
            sb.AppendLine();
            if (byDay.TryGetValue(day, out var meals) && meals.Count > 0)
            {
                sb.Append($"• {dayName}: {string.Join(", ", meals)}");
            }
            else
            {
                sb.Append($"• {dayName}: —");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the main day-list keyboard (7 day buttons + prev/next navigation row).
    /// </summary>
    /// <param name="localizer">The localizer.</param>
    /// <param name="chatId">The chat ID for localization.</param>
    /// <param name="monday">The Monday of the week being displayed.</param>
    /// <returns>An inline keyboard markup.</returns>
    internal static InlineKeyboardMarkup BuildDayListKeyboard(ILocalizer localizer, long chatId, DateOnly monday)
    {
        var weekStart = monday.ToString("yyyy-MM-dd");
        var keyboard = new List<List<InlineKeyboardButton>>();
        var row = new List<InlineKeyboardButton>();

        for (int day = 1; day <= 7; day++)
        {
            var dayName = localizer.Get(chatId, $"week.day.{day}");
            row.Add(InlineKeyboardButton.WithCallbackData(dayName, $"week:day:{weekStart}:{day}"));

            if (row.Count == 3 || day == 7)
            {
                keyboard.Add(row);
                row = new List<InlineKeyboardButton>();
            }
        }

        var prevMonday = monday.AddDays(-7);
        var nextMonday = monday.AddDays(7);
        var prevRange = FormatWeekRange(prevMonday, localizer, chatId);
        var nextRange = FormatWeekRange(nextMonday, localizer, chatId);
        var prevLabel = string.Format(localizer.Get(chatId, "week.btn-prev-week"), prevRange);
        var nextLabel = string.Format(localizer.Get(chatId, "week.btn-next-week"), nextRange);

        keyboard.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData(prevLabel, $"week:nav:{prevMonday:yyyy-MM-dd}"),
            InlineKeyboardButton.WithCallbackData(nextLabel, $"week:nav:{nextMonday:yyyy-MM-dd}"),
        });

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
    /// <param name="weekStartDate">The ISO-8601 week start date (yyyy-MM-dd).</param>
    /// <returns>An inline keyboard markup.</returns>
    internal static InlineKeyboardMarkup BuildDayDetailKeyboard(
        int day,
        IReadOnlyList<DayMealEntry> dayMeals,
        bool hasMeals,
        ILocalizer localizer,
        long chatId,
        string weekStartDate)
    {
        var keyboard = new List<List<InlineKeyboardButton>>();

        foreach (var entry in dayMeals)
        {
            keyboard.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(entry.MealName, "week:noop"),
                InlineKeyboardButton.WithCallbackData(
                    localizer.Get(chatId, "week.btn-clear"),
                    $"week:clear:{weekStartDate}:{day}:{entry.MealId}"),
            });
        }

        if (hasMeals && dayMeals.Count < MaxMealsPerDay)
        {
            keyboard.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    localizer.Get(chatId, "week.btn-add-meal"),
                    $"week:pick:{weekStartDate}:{day}"),
            });
        }

        keyboard.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData(
                localizer.Get(chatId, "week.btn-back"),
                $"week:back:{weekStartDate}"),
            InlineKeyboardButton.WithCallbackData(
                localizer.Get(chatId, "week.btn-to-cart"),
                $"week:to_cart:{weekStartDate}:{day}"),
        });

        return new InlineKeyboardMarkup(keyboard);
    }
}
