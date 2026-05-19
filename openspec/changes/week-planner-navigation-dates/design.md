# Week Planner Navigation & Dates — Design

## Overview

Anchor every week view to a real calendar week (ISO week, Monday = day 1). Embed the week's Monday date (`yyyy-MM-dd`) in all callback data so navigation is stateless. The header shows the date range; two nav buttons shift one week at a time.

## Week Anchor Convention

- **Week start**: Monday (ISO 8601).
- **`WeekStartDate`** stored as `yyyy-MM-dd` of that Monday (e.g., `2026-05-18`).
- Computed in C# from `DateOnly.FromDateTime(DateTime.UtcNow)` adjusted to previous or current Monday.

```csharp
// Get Monday of the week containing `date`
static DateOnly GetWeekMonday(DateOnly date)
{
    int offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
    return date.AddDays(-offset);
}
```

- `/week` with no argument → `GetWeekMonday(DateOnly.FromDateTime(DateTime.UtcNow))`.

## UI Flow

### Main Week View (`/week` or `week:nav:{weekStart}`)

```
📅 Meal plan: May 18–24, 2026

[Mon]  [Tue]  [Wed]
[Thu]  [Fri]  [Sat]
[Sun]
[← May 11–17]   [May 25–31 →]
```

- Header format: `localizer.Get(chatId, "week.header")` + `" " + FormatWeekRange(monday, localizer, chatId)`.
- 7 day buttons in rows of 3 (unchanged layout) — `week:day:{weekStart}:{day}`.
- Bottom nav row: prev-week button + next-week button.
  - Prev: `week:nav:{prevMonday}` labelled with prev range.
  - Next: `week:nav:{nextMonday}` labelled with next range.
- Week range formatting (helper `FormatWeekRange`):
  - Same month: `"May 18–24, 2026"`
  - Cross-month: `"May 29 – Jun 4, 2026"`
  - Month abbreviations from localizer keys `week.month.1` … `week.month.12`.

### Day View (`week:day:{weekStart}:{day}`)

Unchanged layout from previous design; `weekStart` flows through all sub-callbacks.

```
📅 Monday, May 18:
• Pasta
[Pasta]          [✕]
[＋ Add meal]
[← Back]  [🛒 Add ingredients to cart]
```

Day header now includes the date: `"📅 {dayName}, {dayDate}:"` where `dayDate = monday.AddDays(day - 1)` formatted as `"May 18"`.

### Meal Picker (`week:pick:{weekStart}:{day}`)

Back button returns to `week:day:{weekStart}:{day}`. Otherwise unchanged.

### Add to Cart (`week:to_cart:{weekStart}:{day}`)

Unchanged logic; scoped to `weekStartDate`.

## Callback Data Format

| Callback | Max length (bytes) | Notes |
|---|---|---|
| `week:nav:2026-05-18` | 19 | Navigate to week |
| `week:day:2026-05-18:7` | 21 | Open day detail |
| `week:pick:2026-05-18:7` | 22 | Show meal picker |
| `week:assign:2026-05-18:7:9999` | 30 | Assign meal |
| `week:clear:2026-05-18:7:9999` | 29 | Clear specific meal |
| `week:to_cart:2026-05-18:7` | 25 | Add to shopping cart |
| `week:back:2026-05-18` | 20 | Return to week view |
| `week:noop` | 9 | Label button |

All are under 64 bytes ✓. No session keying needed.

## Sequence Diagram — Navigation

```
User taps [May 25–31 →]
  → CallbackQuery.Data = "week:nav:2026-05-25"
  → WeekCallbackHandler.HandleAsync
      → action = "nav", weekStart = 2026-05-25
      → DayMealsRepository.GetWeekAsync(groupId, "2026-05-25")
      → WeekViewBuilder.BuildDayListKeyboard(localizer, chatId, monday2026-05-25)
      → botClient.EditMessageText(header + range, keyboard)
```

## Schema Migration

Add `WeekStartDate TEXT NOT NULL DEFAULT ''` to `DayMeals` via the standard additive pattern:

```csharp
try
{
    var pragma = connection.CreateCommand();
    pragma.CommandText = "PRAGMA table_info(DayMeals)";
    // read columns, check if WeekStartDate exists
    // if not:
    var alter = connection.CreateCommand();
    alter.CommandText = "ALTER TABLE DayMeals ADD COLUMN WeekStartDate TEXT NOT NULL DEFAULT ''";
    await alter.ExecuteNonQueryAsync();
}
catch (SqliteException ex) when (ex.SqliteErrorCode == 1) { /* already exists */ }
```

Existing rows get `WeekStartDate = ''`; they are invisible to date-scoped queries (`WHERE WeekStartDate = @w` won't match `''` unless `@w` is also `''`).

## Repository API Changes

```csharp
// All methods gain a weekStartDate parameter:
Task<List<DayMealEntry>> GetWeekAsync(int groupId, string weekStartDate)
Task InsertAsync(int groupId, int dayOfWeek, int mealId, string weekStartDate)
Task<int> GetCountAsync(int groupId, int dayOfWeek, string weekStartDate)
Task ClearAsync(int groupId, int dayOfWeek, string weekStartDate)
Task ClearMealAsync(int groupId, int dayOfWeek, int mealId, string weekStartDate)
```

SQL examples:
```sql
-- GetWeekAsync
SELECT dm.DayOfWeek, dm.MealId, m.Name
FROM DayMeals dm JOIN Meals m ON dm.MealId = m.Id
WHERE dm.GroupId = @groupId AND dm.WeekStartDate = @weekStartDate
ORDER BY dm.DayOfWeek, dm.Id

-- InsertAsync
INSERT INTO DayMeals (GroupId, DayOfWeek, MealId, WeekStartDate)
VALUES (@groupId, @dayOfWeek, @mealId, @weekStartDate)
```

## Localization Keys

New keys:
- `week.week-range` — not used directly; formatting done in `FormatWeekRange` helper
- `week.btn-prev-week` — `"← {0}"` (previous week date range substituted at `{0}`)
- `week.btn-next-week` — `"{0} →"` (next week date range substituted at `{0}`)
- `week.month.1` … `week.month.12` — short month names ("Jan"…"Dec") for date range formatting
- `week.day-header` — `"📅 {0}, {1}:"` (day name, day date like "May 18")

## File Changes

| File | Change |
|---|---|
| `Database/DatabaseInitializer.cs` | Add `WeekStartDate` column migration |
| `Repositories/DayMealsRepository.cs` | Add `weekStartDate` param to all 5 methods |
| `Handlers/WeekViewBuilder.cs` | Add `FormatWeekRange`, `FormatDayDate`; update `BuildDayListKeyboard` to accept `DateOnly monday` + add nav row; update `BuildDayDetailKeyboard` to accept `weekStartDate` in callbacks |
| `Handlers/WeekCommandHandler.cs` | Compute current-week Monday; pass to builder and repository |
| `Handlers/WeekCallbackHandler.cs` | Parse `weekStartDate` from all callbacks; add `nav` action; pass date to repository and builder |
| `Localization/Strings.en.json` | Add 16 new keys (2 nav + 12 months + day-header) |
| `Localization/Strings.ru.json` | Same |
| `Localization/Strings.pl.json` | Same |
| `Tests/Handlers/WeekCommandHandlerTests.cs` | Update for date-scoped calls |
| `Tests/Handlers/WeekCallbackHandlerTests.cs` | Update all tests for new callback format |
| `Tests/Integration/WeekIntegrationTests.cs` | Update for date-scoped callbacks |
