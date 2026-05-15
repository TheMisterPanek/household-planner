## 1. Add Localization Keys

- [ ] 1.1 Add all `weekplan.*` keys to `Strings.en.json`:
  - `weekplan.header`, `weekplan.pick-prompt`, `weekplan.no-meals`, `weekplan.btn-set`, `weekplan.btn-clear`, `weekplan.btn-back`
  - `weekplan.day.1` through `weekplan.day.7` (Mon–Sun)
- [ ] 1.2 Add the same keys with Russian translations to `Strings.ru.json`
- [ ] 1.3 Add the same keys with Polish translations to `Strings.pl.json`

## 2. Database Schema

- [ ] 2.1 In `DatabaseInitializer.StartAsync`, add the `WeekMealPlan` table DDL after the existing `MealSteps` block:
  ```sql
  CREATE TABLE IF NOT EXISTS WeekMealPlan (
      Id            INTEGER PRIMARY KEY AUTOINCREMENT,
      GroupId       INTEGER NOT NULL REFERENCES Groups(Id),
      WeekStartDate TEXT    NOT NULL,
      DayOfWeek     INTEGER NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),
      MealId        INTEGER NOT NULL REFERENCES Meals(Id),
      UNIQUE(GroupId, WeekStartDate, DayOfWeek)
  );
  ```

## 3. WeekMealPlanRepository

- [ ] 3.1 Create `ProductTrackerBot/Repositories/WeekMealPlanRepository.cs`:
  - `GetWeekAsync(int groupId, string weekStartDate) → Task<List<WeekMealPlanEntry>>` — JOIN with `Meals` to get `MealName`, ORDER BY `DayOfWeek`
  - `UpsertAsync(int groupId, string weekStartDate, int dayOfWeek, int mealId) → Task` — `INSERT OR REPLACE`
  - `ClearAsync(int groupId, string weekStartDate, int dayOfWeek) → Task` — `DELETE`
  - All methods `virtual` for mockability
- [ ] 3.2 Create `ProductTrackerBot/Models/WeekMealPlanEntry.cs` — record with `int DayOfWeek`, `int MealId`, `string MealName`
- [ ] 3.3 Register `WeekMealPlanRepository` as `builder.Services.AddScoped<WeekMealPlanRepository>()` in `Program.cs`

## 4. WeekPlanCommandHandler

- [ ] 4.1 Create `ProductTrackerBot/Handlers/WeekPlanCommandHandler.cs` implementing `ICommandHandler`:
  - `Command = "/weekplan"`
  - `Description = "View and plan meals for the week"`
  - Guard: non-group chats reply `localizer.Get(chatId, "common.group-only")` and return
  - Compute `weekStart` via `GetCurrentWeekMonday()` (static helper — see design.md)
  - Fetch plan via `WeekMealPlanRepository.GetWeekAsync`
  - Fetch meals via `MealRepository.GetAllAsync`
  - Build keyboard via `BuildWeekKeyboard` (private static helper)
  - Send message with `localizer.Get(chatId, "weekplan.header")` and the keyboard
- [ ] 4.2 Register handler: `builder.Services.AddScoped<ICommandHandler, WeekPlanCommandHandler>()` in `Program.cs`

## 5. WeekPlanCallbackHandler

- [ ] 5.1 Create `ProductTrackerBot/Handlers/WeekPlanCallbackHandler.cs` implementing `ICallbackHandler`:
  - `CallbackPrefix = "week:"`
  - Parse `parts = data.Split(':')`, dispatch on `parts[1]`
  - **`pick`**: fetch meals, build meal-picker keyboard (`week:assign:…` per meal + back button), edit message
  - **`assign`**: call `UpsertAsync`, then `RenderWeekViewAsync` (private helper that fetches plan + meals, builds keyboard, edits message)
  - **`clear`**: call `ClearAsync`, then `RenderWeekViewAsync`
  - **`back`**: call `RenderWeekViewAsync`
  - **`noop`**: answer callback only (no-op, for display-only day-name buttons)
  - `BuildWeekKeyboard` (private static helper shared with command handler — extract to internal `WeekPlanRenderer` static class if preferred)
  - `RenderWeekViewAsync` (private async helper — fetch plan, fetch meals, edit message)
- [ ] 5.2 Register handler: `builder.Services.AddScoped<ICallbackHandler, WeekPlanCallbackHandler>()` in `Program.cs`

## 6. Unit Tests — WeekMealPlanRepository

- [ ] 6.1 `GetWeekAsync` returns empty list when no rows exist for the given week
- [ ] 6.2 `GetWeekAsync` returns correct entries with meal names for a populated week
- [ ] 6.3 `UpsertAsync` inserts a new row; subsequent `GetWeekAsync` returns it
- [ ] 6.4 `UpsertAsync` replaces existing row for same `(GroupId, WeekStartDate, DayOfWeek)` with a new `MealId`
- [ ] 6.5 `ClearAsync` removes the row; subsequent `GetWeekAsync` does not return the deleted day

All tests use in-memory SQLite (`:memory:` connection string) and must create both `Groups`, `Meals`, and `WeekMealPlan` tables in the arrange step.

## 7. Unit Tests — WeekPlanCommandHandler

- [ ] 7.1 Private chat → replies "group chat only" message; no repository calls made
- [ ] 7.2 Group with no meals and empty plan → sends message with header; each day row has day-name button but no [＋ Set] button
- [ ] 7.3 Group with meals and empty plan → sends message; each day row shows `—` and `[＋ Set]` button with callback `week:pick:{day}:{weekStart}`
- [ ] 7.4 Group with a plan entry (e.g., Monday = "Pasta") → Monday row shows "Pasta" label and `[✕]` button with callback `week:clear:1:{weekStart}`; other days show `—` + `[＋ Set]`

Use `Mock<WeekMealPlanRepository>()` and `Mock<MealRepository>()`. `ILocalizer` mock returns key names.

## 8. Unit Tests — WeekPlanCallbackHandler

- [ ] 8.1 **`noop` action** → `AnswerCallbackQuery` called; no repository calls; no `EditMessageText` call
- [ ] 8.2 **`pick` action, group has meals** → `EditMessageText` called; keyboard contains one button per meal with `week:assign:…` callback and a back button
- [ ] 8.3 **`pick` action, group has no meals** → `AnswerCallbackQuery` called with `weekplan.no-meals` text; `EditMessageText` not called
- [ ] 8.4 **`assign` action** → `UpsertAsync` called with correct args; `EditMessageText` called (week view re-rendered)
- [ ] 8.5 **`clear` action** → `ClearAsync` called with correct args; `EditMessageText` called (week view re-rendered)
- [ ] 8.6 **`back` action** → no repository write; `EditMessageText` called (week view re-rendered)

## 9. Manual Testing

- [ ] 9.1 `/weekplan` in a private chat → "group only" response
- [ ] 9.2 `/weekplan` in group with no meals → week view shows 7 day rows, none have [＋ Set]
- [ ] 9.3 `/weekplan` in group with meals → week view shows 7 day rows, each has [＋ Set]
- [ ] 9.4 Tap [＋ Set] on Wednesday → meal-picker appears with all group meals as buttons + [← Back]
- [ ] 9.5 Tap a meal in picker → week view refreshed, Wednesday row now shows meal name + [✕]
- [ ] 9.6 Tap [✕] on Wednesday → week view refreshed, Wednesday row shows `—` + [＋ Set] again
- [ ] 9.7 Tap [← Back] in picker → week view shown without changes
- [ ] 9.8 `/weekplan` again → persisted assignments still visible
- [ ] 9.9 Assign different meal to same day → previous meal replaced (INSERT OR REPLACE)
- [ ] 9.10 All existing commands (`/meals`, `/buy`, `/list`, `/history`, `/ai`) unaffected
