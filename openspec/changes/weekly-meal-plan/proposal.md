## Why

The group's `/meals` command manages a recipe library but there is no way to say "we're cooking X on Monday, Y on Wednesday". Meal planning happens outside the bot today. This change adds a `/week` command that shows the seven abstract day slots (Mon–Sun) and lets any group member assign a meal from the library to each day. No calendar dates, no week navigation — just a persistent Mon–Sun plan the group can set and update at any time.

The week concept is intentionally left as a thin shell: it is a display aggregation of day slots only. Future scope (shopping-cart rollup from the week's meals) will build on top of this layer without changing the day-assignment model.

## What Changes

- **`DayMeals` table** (new): `(Id, GroupId, DayOfWeek INTEGER CHECK 1–7, MealId INTEGER REFERENCES Meals(Id), UNIQUE(GroupId, DayOfWeek))`. One row per group per day; no date column. `UpsertAsync` uses `INSERT OR REPLACE`.
- **`DayMealsRepository`** (new): `GetWeekAsync`, `UpsertAsync`, `ClearAsync`. Registered as `AddScoped`.
- **`WeekCommandHandler`** (new, `ICommandHandler`): Handles `/week`. Fetches the group's 7-day plan and its meal library; renders a day-by-day inline keyboard.
- **`WeekCallbackHandler`** (new, `ICallbackHandler`, prefix `week:`): Sub-actions:
  - `week:pick:{day}` — edits the message to a meal-picker for that day.
  - `week:assign:{day}:{mealId}` — upserts the plan row, renders the updated week view.
  - `week:clear:{day}` — removes the plan row, renders the updated week view.
  - `week:back` — returns to the week view without changes.
  - `week:noop` — no-op for display-only buttons.
- **Localization keys** (new): `week.*` keys in `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`.

## Capabilities

### New Capabilities

- `view-week-plan`: User sends `/week` and sees Mon–Sun, each day showing its assigned meal or "—", with a [＋ Set] button for unset days.
- `assign-meal-to-day`: Tapping [＋ Set] on a day shows the meal library as inline buttons. Tapping a meal assigns it and returns to the week view.
- `clear-meal-from-day`: Each assigned day shows a [✕] button that removes the assignment and returns to the week view.

## Out of Scope (future)

- Calendar-date anchoring or weekly snapshots.
- Week-as-shopping-cart (aggregate all day ingredients into the shopping list).
- Week navigation (prev/next week).

## Impact

- **Code**: 3 new files; 3 existing files modified (`DatabaseInitializer`, `Program.cs`, localization JSONs).
- **APIs**: No change to external API surface.
- **Dependencies**: No new NuGet packages.
- **Systems**: One new SQLite table (`DayMeals`); additive migration only.
- **Compatibility**: Fully AOT-safe — raw `SqliteCommand`, no reflection, callback data parsed via `string.Split`.

## Rollback Plan

1. Remove `DayMealsRepository.cs`, `WeekCommandHandler.cs`, `WeekCallbackHandler.cs`.
2. Remove the `DayMeals` DDL block from `DatabaseInitializer.StartAsync` (table remains in DB but causes no harm).
3. Revert the `AddScoped` registrations added to `Program.cs`.
4. Remove new `week.*` localization keys from `Strings.*.json`.

## Affected Teams

Single-user/household bot. No shared infrastructure impact.

## Cross-Cutting Notes

- `WeekCommandHandler` is group-only: private-chat invocations reply via `localizer.Get(chatId, "common.group-only")` and return.
- `DayMealsRepository` is `AddScoped` (stateless DB calls).
- Callback data is short: longest form `week:assign:7:9999` = 18 bytes, well within the 64-byte limit.
- All user-facing strings flow through `ILocalizer`; no hardcoded literals.
- No `IHistoryRepository.RecordAsync` call required — day assignments are soft preferences, not auditable inventory mutations.
- No new types need to be registered in `BotActionPayloadContext`.
