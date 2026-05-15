## Why

The group's `/meals` command manages a library of named meals, but there is no way to plan *which* meals will be cooked on which day of the upcoming week. Meal planning currently happens outside the bot (in chat messages, shared notes, etc.), which means the bot cannot show the week's grocery needs in context.

This change adds a `/weekplan` command that lets any group member assign existing meals to days of the current week (Monday–Sunday). Every day can hold one meal. Assignments use inline keyboard buttons drawn from the group's own meal library — no free-text entry, no typos.

## What Changes

- **`WeekMealPlan` table** (new): `(Id, GroupId, WeekStartDate TEXT, DayOfWeek INTEGER, MealId INTEGER REFERENCES Meals(Id))`. One row per group per day per week, unique on `(GroupId, WeekStartDate, DayOfWeek)`. `WeekStartDate` stores the ISO-8601 date of the week's Monday.
- **`WeekMealPlanRepository`** (new): `GetWeekAsync`, `UpsertAsync`, `ClearAsync`. Registered behind its own interface.
- **`WeekPlanCommandHandler`** (new, `ICommandHandler`): Handles `/weekplan`. Computes current week's Monday, fetches plan and meals, renders a day-by-day inline keyboard.
- **`WeekPlanCallbackHandler`** (new, `ICallbackHandler`, prefix `week:`): Two sub-actions:
  - `week:pick:{dayOfWeek}:{weekStart}` — edits the message to a meal-picker list for that day.
  - `week:assign:{dayOfWeek}:{weekStart}:{mealId}` — upserts the plan row, then renders the updated week view.
  - `week:clear:{dayOfWeek}:{weekStart}` — removes the plan row, then renders the updated week view.
  - `week:back:{weekStart}` — returns to the week view from the meal-picker without making changes.
- **Localization keys** (new): `weekplan.*` keys in `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`.

## Capabilities

### New Capabilities

- `view-week-meal-plan`: User sends `/weekplan` and sees the current week (Mon–Sun) with each day's assigned meal or "(not set)", plus a [Set] button per day.
- `assign-meal-to-day`: Tapping [Set] on a day shows the group's meal library as inline buttons. Tapping a meal assigns it to that day and returns to the week view.
- `clear-meal-from-day`: Each assigned day shows a [✕ Clear] button that removes the assignment.

## Impact

- **Code**: 3 new files; 3 existing files modified (`DatabaseInitializer`, `Program.cs`, localization JSONs).
- **APIs**: No change to external API surface.
- **Dependencies**: No new NuGet packages.
- **Systems**: One new SQLite table (`WeekMealPlan`); additive migration only.
- **Compatibility**: Fully AOT-safe — no reflection, no dynamic dispatch; all new callback payload types registered in `BotActionPayloadContext`.

## Rollback Plan

1. Remove `WeekMealPlanRepository.cs`, `WeekPlanCommandHandler.cs`, `WeekPlanCallbackHandler.cs`.
2. Remove the `WeekMealPlan` table DDL block from `DatabaseInitializer.StartAsync` (the table will remain in existing databases but causes no harm).
3. Revert the three `AddScoped` registrations added to `Program.cs`.
4. Remove new `weekplan.*` localization keys from `Strings.*.json`.

`UpdateDispatcher`, all other handlers, and the existing `Meals`/`MealIngredients`/`MealSteps` tables are untouched.

## Affected Teams

Single-user/household bot. No shared infrastructure impact.

## Cross-Cutting Notes

- `WeekPlanCommandHandler` is group-only: private-chat invocations reply with the standard "group chat only" message via localizer and exit early.
- `WeekMealPlanRepository` is registered as `AddScoped` (stateless DB calls).
- Callback data stays well within the 64-byte limit: longest form is `week:assign:7:2026-05-11:9999` = 30 bytes.
- All user-facing strings (day names, button labels, empty-state text) flow through `ILocalizer`; no hardcoded literals.
- The `/weekplan` view shows only the current ISO week (Monday–Sunday). Week navigation (prev/next) is out of scope for this change.
- No `IHistoryRepository.RecordAsync` call is required for meal-plan assignments — these are soft preferences, not auditable inventory mutations. If audit policy changes later, a `MealPlanned` action type can be added without touching this handler.
- `WeekStartDate` is computed in UTC using `DateTimeOffset.UtcNow`; the Monday of that UTC day is used as the anchor.
