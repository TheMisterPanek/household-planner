## 1. Add Localization Keys

- [ ] 1.1 Add all `week.*` keys to `Strings.en.json`:
  - `week.header`, `week.pick-prompt`, `week.no-meals`
  - `week.btn-set`, `week.btn-clear`, `week.btn-back`
  - `week.day.1` through `week.day.7` (Mon–Sun)
- [ ] 1.2 Add the same keys with Russian translations to `Strings.ru.json`
- [ ] 1.3 Add the same keys with Polish translations to `Strings.pl.json`

## 2. Database Schema

- [ ] 2.1 In `DatabaseInitializer.StartAsync`, add the `DayMeals` table DDL after the existing `MealSteps` block:
  ```sql
  CREATE TABLE IF NOT EXISTS DayMeals (
      Id        INTEGER PRIMARY KEY AUTOINCREMENT,
      GroupId   INTEGER NOT NULL REFERENCES Groups(Id),
      DayOfWeek INTEGER NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),
      MealId    INTEGER NOT NULL REFERENCES Meals(Id),
      UNIQUE(GroupId, DayOfWeek)
  );
  ```

## 3. DayMealsRepository

- [ ] 3.1 Create `ProductTrackerBot/Models/DayMealEntry.cs` — record with `int DayOfWeek`, `int MealId`, `string MealName`
- [ ] 3.2 Create `ProductTrackerBot/Repositories/DayMealsRepository.cs`:
  - `GetWeekAsync(int groupId) → Task<List<DayMealEntry>>` — JOIN with `Meals`, ORDER BY `DayOfWeek`
  - `UpsertAsync(int groupId, int dayOfWeek, int mealId) → Task` — `INSERT OR REPLACE`
  - `ClearAsync(int groupId, int dayOfWeek) → Task` — `DELETE`
  - All methods `virtual` for mockability
- [ ] 3.3 Register: `builder.Services.AddScoped<DayMealsRepository>()` in `Program.cs`

## 4. WeekCommandHandler

- [ ] 4.1 Create `ProductTrackerBot/Handlers/WeekCommandHandler.cs` implementing `ICommandHandler`:
  - `Command = "/week"`, `Description = "View and plan meals for the week"`
  - Guard: non-group chats reply `localizer.Get(chatId, "common.group-only")` and return
  - Fetch plan via `DayMealsRepository.GetWeekAsync`; fetch meals via `MealRepository.GetAllAsync`
  - Build week keyboard via `BuildWeekKeyboard` (private static helper — see design.md for row logic)
  - Send message with `localizer.Get(chatId, "week.header")` and the keyboard
- [ ] 4.2 Register: `builder.Services.AddScoped<ICommandHandler, WeekCommandHandler>()` in `Program.cs`

## 5. WeekCallbackHandler

- [ ] 5.1 Create `ProductTrackerBot/Handlers/WeekCallbackHandler.cs` implementing `ICallbackHandler`:
  - `CallbackPrefix = "week:"`
  - Parse `data.Split(':')`, dispatch on `parts[1]`: `pick`, `assign`, `clear`, `back`, `noop`
  - **`pick`**: fetch meals, build meal-picker keyboard (`week:assign:{day}:{mealId}` per meal + `week:back` back button), edit message
  - **`assign`**: `UpsertAsync`, then `RenderWeekViewAsync`
  - **`clear`**: `ClearAsync`, then `RenderWeekViewAsync`
  - **`back`**: `RenderWeekViewAsync`
  - **`noop`**: answer callback only
  - `BuildWeekKeyboard` and `RenderWeekViewAsync` as private helpers (share keyboard logic with `WeekCommandHandler` via a static `WeekViewBuilder` internal class if desired)
- [ ] 5.2 Register: `builder.Services.AddScoped<ICallbackHandler, WeekCallbackHandler>()` in `Program.cs`

## 6. Unit Tests — DayMealsRepository

- [ ] 6.1 `GetWeekAsync` returns empty list when no rows exist for the group
- [ ] 6.2 `GetWeekAsync` returns correct entries with meal names for a seeded plan
- [ ] 6.3 `UpsertAsync` inserts a new row; subsequent `GetWeekAsync` returns it
- [ ] 6.4 `UpsertAsync` replaces existing row for same `(GroupId, DayOfWeek)` with a new `MealId`
- [ ] 6.5 `ClearAsync` removes the row; subsequent `GetWeekAsync` does not return that day

All tests use in-memory SQLite (`:memory:`) and must create `Groups`, `Meals`, and `DayMeals` tables in the arrange step.

## 7. Unit Tests — WeekCommandHandler

- [ ] 7.1 Private chat → replies "group chat only"; no repository calls
- [ ] 7.2 Group with no meals and empty plan → sends message with header; day rows have no [＋ Set] button
- [ ] 7.3 Group with meals and empty plan → sends message; each day row includes `week:pick:{day}` callback
- [ ] 7.4 Group with a plan entry (Monday = "Pasta") → Monday row has `week:clear:1` callback; other days have `week:pick:{day}`

Use `Mock<DayMealsRepository>()` and `Mock<MealRepository>()`. `ILocalizer` mock returns key names.

## 8. Unit Tests — WeekCallbackHandler

- [ ] 8.1 **`noop`** → `AnswerCallbackQuery` called; no repository calls; no `EditMessageText`
- [ ] 8.2 **`pick`, group has meals** → `EditMessageText` called; keyboard contains one `week:assign:{day}:{id}` button per meal and a `week:back` button
- [ ] 8.3 **`pick`, no meals** → `AnswerCallbackQuery` called with `week.no-meals`; no `EditMessageText`
- [ ] 8.4 **`assign`** → `UpsertAsync` called with correct args; `EditMessageText` called (week view re-rendered)
- [ ] 8.5 **`clear`** → `ClearAsync` called with correct args; `EditMessageText` called (week view re-rendered)
- [ ] 8.6 **`back`** → no repository write; `EditMessageText` called (week view re-rendered)

## 9. Integration Test

- [ ] 9.1 Wire `WeekCommandHandler`, `WeekCallbackHandler`, and `DayMealsRepository` into `TelegramIntegrationTestBase`
- [ ] 9.2 `/week` in private chat → bot sends "group chat only" message
- [ ] 9.3 `/week` in group with no meals → bot sends week view; no assign buttons present
- [ ] 9.4 `/week` in group with one meal → bot sends week view; Monday row has `week:pick:1` callback
- [ ] 9.5 Dispatch `week:pick:1` callback → bot edits message with meal-picker keyboard containing the meal
- [ ] 9.6 Dispatch `week:assign:1:{mealId}` callback → `DayMealsRepository.GetWeekAsync` returns an entry for Monday; bot edits message to week view showing the meal name
- [ ] 9.7 Dispatch `week:clear:1` callback → `DayMealsRepository.GetWeekAsync` returns no entry for Monday; bot edits message to week view showing "—"

## 10. Smoke Test (manual — do not mark complete without user confirmation)

- [ ] 10.1 `/week` in a private chat → "group only" response
- [ ] 10.2 `/week` in group with no meals → 7-day view; no [＋ Set] buttons
- [ ] 10.3 `/week` in group with at least one meal → 7-day view; each day has [＋ Set]
- [ ] 10.4 Tap [＋ Set] on Wednesday → meal picker appears with group meals + [← Back]
- [ ] 10.5 Tap a meal in picker → week view refreshed, Wednesday shows meal name + [✕]
- [ ] 10.6 Tap [✕] on Wednesday → week view refreshed, Wednesday shows "—" + [＋ Set]
- [ ] 10.7 Tap [← Back] in picker → week view unchanged
- [ ] 10.8 Send `/week` again → persisted assignments still visible
- [ ] 10.9 Assign different meal to same day → previous meal replaced (INSERT OR REPLACE)
- [ ] 10.10 Existing commands (`/meals`, `/buy`, `/list`, `/history`, `/ai`) unaffected
