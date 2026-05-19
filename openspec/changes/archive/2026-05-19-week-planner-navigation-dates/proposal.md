## Why

The current `/week` planner is completely abstract — it shows Mon–Sun with no calendar anchor. You can't tell which actual dates the plan covers, and there's no way to plan ahead. The motivating use case: you'll run out of bread in 10 days and want to plan to buy/bake it next week. Right now the planner gives you a single floating week with no date context, so future planning is impossible.

This change anchors the week view to real calendar weeks (ISO weeks, Mon–Sun), shows the date range prominently, and adds Prev / Next navigation buttons so any group member can look at past meals or plan meals for upcoming weeks independently.

## What Changes

- **`DayMeals` schema**: Add `WeekStartDate TEXT NOT NULL DEFAULT ''` column (ISO-8601 `yyyy-MM-dd` of the week's Monday). Additive migration via `PRAGMA table_info` + `ALTER TABLE ADD COLUMN`. Existing rows keep empty-string week date (legacy; not shown in date-scoped views).
- **`DayMealsRepository`**: All query/insert methods gain a `weekStartDate` parameter to scope data to the target week.
- **`WeekViewBuilder`**: Header now shows "📅 Meal plan: May 18–24, 2026". Week-list keyboard gains a navigation row with `[← Prev]` / `[Next →]` buttons.
- **`WeekCallbackHandler`**: New `nav` action for prev/next navigation. All existing actions (`day`, `pick`, `assign`, `clear`, `to_cart`, `back`) have `weekStartDate` embedded in callback data.
- **`WeekCommandHandler`**: Defaults to current ISO week (today's Monday).
- **Localization keys**: New `week.btn-prev-week`, `week.btn-next-week`, `week.week-range` keys.

## Capabilities

### New Capabilities

- `week-date-header`: `/week` shows the week's date range in the header (e.g., "📅 Meal plan: May 18–24, 2026").
- `week-navigate-prev`: Tapping `← {date range}` button renders the same week view for the previous week.
- `week-navigate-next`: Tapping `{date range} →` button renders the same week view for the next week.
- `week-plan-by-date`: Meal assignments are stored per calendar week; different weeks have independent plans.

### Modified Capabilities

- `view-week-plan`: Now shows a specific calendar week (defaults to current) instead of an abstract Mon–Sun slot.
- `assign-meal-to-day`: Assignments are scoped to the target week and persist correctly when navigating away and back.
- `clear-meal-from-day`: Scoped to the target week.

## Out of Scope

- Maximum past/future range limiting (any week is navigable).
- Copying a week's plan to another week.
- Merging legacy (no-date) rows into the current week.

## Impact

- **Code**: 2 files modified significantly (`DayMealsRepository`, `WeekCallbackHandler`), 2 files minor changes (`WeekViewBuilder`, `WeekCommandHandler`), 3 localization files.
- **Schema**: One new column on `DayMeals`; additive only.
- **Callback data**: Longest new form `week:assign:2026-05-18:7:9999` = 30 bytes — within 64-byte limit.
- **APIs**: No external surface change.
- **Dependencies**: No new NuGet packages.

## Rollback Plan

1. Revert `DayMealsRepository` method signatures (remove `weekStartDate` param).
2. Revert `WeekViewBuilder`, `WeekCallbackHandler`, `WeekCommandHandler` to previous versions.
3. Remove new localization keys from `Strings.*.json`.
4. The `WeekStartDate` column remains in the DB but causes no harm — rows for the abstract plan have `''` and are unaffected.

## Affected Teams

Single-user/household bot. No shared infrastructure impact.

## Cross-Cutting Notes

- All queries scope by `WeekStartDate`; the `''`-default migration pattern is intentional and safe for existing rows.
- Callback data sizes verified against 64-byte limit (longest: 30 bytes ✓).
- All user-facing strings flow through `ILocalizer`; no hardcoded literals.
- Week-navigation is group-only; private-chat invocations are rejected upstream in `WeekCommandHandler`.
- No `IHistoryRepository.RecordAsync` call needed — day assignments remain soft preferences.
- No new types in `BotActionPayloadContext`; all parsing via `string.Split`.
- AOT-safe: raw `SqliteCommand`, `string.Split` parsing, no reflection.
