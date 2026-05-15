## Schema

### New table: `WeekMealPlan`

Added in `DatabaseInitializer.StartAsync` using the standard `CREATE TABLE IF NOT EXISTS` pattern:

```sql
CREATE TABLE IF NOT EXISTS WeekMealPlan (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    GroupId       INTEGER NOT NULL REFERENCES Groups(Id),
    WeekStartDate TEXT    NOT NULL,   -- ISO-8601 date of the Monday, e.g. "2026-05-11"
    DayOfWeek     INTEGER NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),  -- 1=Mon, 7=Sun
    MealId        INTEGER NOT NULL REFERENCES Meals(Id),
    UNIQUE(GroupId, WeekStartDate, DayOfWeek)
);
```

`WeekStartDate` is stored as `yyyy-MM-dd` TEXT (lexicographically sortable). The `UNIQUE` constraint enforces one meal per group per day per week; `UpsertAsync` uses `INSERT OR REPLACE`.

---

## Repository: `WeekMealPlanRepository`

```
GetWeekAsync(int groupId, string weekStartDate)
    → List<(int DayOfWeek, int MealId, string MealName)>
    JOIN WeekMealPlan wp ON Meals m ON wp.MealId = m.Id
    WHERE wp.GroupId = @groupId AND wp.WeekStartDate = @weekStart
    ORDER BY wp.DayOfWeek

UpsertAsync(int groupId, string weekStartDate, int dayOfWeek, int mealId)
    INSERT OR REPLACE INTO WeekMealPlan (GroupId, WeekStartDate, DayOfWeek, MealId)
    VALUES (...)

ClearAsync(int groupId, string weekStartDate, int dayOfWeek)
    DELETE FROM WeekMealPlan
    WHERE GroupId = @groupId AND WeekStartDate = @weekStart AND DayOfWeek = @dayOfWeek
```

Registered as `AddScoped<WeekMealPlanRepository>` in `Program.cs`.

---

## Callback Data Format

All callbacks use the `week:` prefix. All fit within the 64-byte Telegram limit.

| Callback data | Example | Bytes | Meaning |
|---|---|---|---|
| `week:pick:{day}:{weekStart}` | `week:pick:1:2026-05-11` | 24 | Open meal picker for a day |
| `week:assign:{day}:{weekStart}:{mealId}` | `week:assign:3:2026-05-11:42` | 29 | Assign meal to a day |
| `week:clear:{day}:{weekStart}` | `week:clear:7:2026-05-11` | 25 | Remove assignment for a day |
| `week:back:{weekStart}` | `week:back:2026-05-11` | 21 | Return to week view |

`{day}` is an integer 1–7 (1=Monday). `{weekStart}` is always `yyyy-MM-dd` of the Monday.

---

## Week Start Computation

```csharp
static string GetCurrentWeekMonday()
{
    var today = DateTimeOffset.UtcNow.Date;
    var daysFromMonday = ((int)today.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
    return today.AddDays(-daysFromMonday).ToString("yyyy-MM-dd");
}
```

---

## Handler: `WeekPlanCommandHandler`

`Command = "/weekplan"`

```
HandleAsync(message):
  if not group chat → reply localizer.Get(chatId, "common.group-only") and return
  group = groupRepository.GetOrCreateAsync(chatId)
  weekStart = GetCurrentWeekMonday()
  plan = weekMealPlanRepository.GetWeekAsync(group.Id, weekStart)
    → dict dayOfWeek → (MealId, MealName)
  meals = mealRepository.GetAllAsync(group.Id)
  keyboard = BuildWeekKeyboard(plan, weekStart, hasMeals: meals.Count > 0)
  text = localizer.Get(chatId, "weekplan.header")
  SendMessage(text, replyMarkup: keyboard)
```

### `BuildWeekKeyboard` logic

One row per day (1–7). Each row has two or three buttons:

- **If day has a meal assigned:**
  `[{DayName}: {MealName}]` (display only, no-op callback `week:noop`) + `[✕]` (`week:clear:{day}:{weekStart}`)
- **If day is unset and group has meals:**
  `[{DayName}: —]` (display only) + `[＋ Set]` (`week:pick:{day}:{weekStart}`)
- **If day is unset and group has no meals:**
  `[{DayName}: —]` (display only, no button — no meals to pick from)

Day names are resolved via localizer keys `weekplan.day.1` … `weekplan.day.7` (Mon–Sun).

---

## Handler: `WeekPlanCallbackHandler`

`CallbackPrefix = "week:"`

```
HandleAsync(callbackQuery):
  parts = data.Split(':')
  action = parts[1]   // "pick" | "assign" | "clear" | "back" | "noop"

  switch action:
    "pick"   → HandlePickAsync(...)
    "assign" → HandleAssignAsync(...)
    "clear"  → HandleClearAsync(...)
    "back"   → HandleBackAsync(...)
    "noop"   → AnswerCallbackQuery() only
```

### `HandlePickAsync(chatId, groupId, dayOfWeek, weekStart)`

```
meals = mealRepository.GetAllAsync(groupId)
if meals.Count == 0:
  AnswerCallbackQuery(localizer.Get(chatId, "weekplan.no-meals"))
  return

keyboard = meals.Select(m =>
    [InlineKeyboardButton(m.Name, $"week:assign:{dayOfWeek}:{weekStart}:{m.Id}")])
  + [[Back button: localizer.Get("weekplan.btn-back"), $"week:back:{weekStart}"]]

dayName = localizer.Get(chatId, $"weekplan.day.{dayOfWeek}")
EditMessageText(localizer.Get(chatId, "weekplan.pick-prompt", dayName), keyboard)
AnswerCallbackQuery()
```

### `HandleAssignAsync(chatId, groupId, dayOfWeek, weekStart, mealId)`

```
weekMealPlanRepository.UpsertAsync(groupId, weekStart, dayOfWeek, mealId)
await RenderWeekViewAsync(chatId, groupId, weekStart, messageId)
AnswerCallbackQuery()
```

### `HandleClearAsync(chatId, groupId, dayOfWeek, weekStart)`

```
weekMealPlanRepository.ClearAsync(groupId, weekStart, dayOfWeek)
await RenderWeekViewAsync(chatId, groupId, weekStart, messageId)
AnswerCallbackQuery()
```

### `HandleBackAsync(chatId, groupId, weekStart)`

```
await RenderWeekViewAsync(chatId, groupId, weekStart, messageId)
AnswerCallbackQuery()
```

### `RenderWeekViewAsync` (private helper)

```
plan = weekMealPlanRepository.GetWeekAsync(groupId, weekStart)
meals = mealRepository.GetAllAsync(groupId)
keyboard = BuildWeekKeyboard(plan, weekStart, hasMeals: meals.Count > 0)
text = localizer.Get(chatId, "weekplan.header")
EditMessageText(chatId, messageId, text, keyboard)
```

---

## Sequence Diagram: Assign a meal to a day

```
User          Bot                WeekMealPlanRepo   MealRepo
 |                |                      |               |
 |--/weekplan---> |                      |               |
 |                |--GetWeekAsync------->|               |
 |                |<--plan---------------|               |
 |                |--GetAllAsync---------|-------------> |
 |                |<--meals--------------|<------------- |
 |                |--SendMessage-------->|               |
 |<--week view----|                      |               |
 |                |                      |               |
 |--[＋ Set Mon]->|   (week:pick:1:...) |               |
 |                |--GetAllAsync---------|-------------> |
 |                |<--meals--------------|<------------- |
 |                |--EditMessage-------->|               |
 |<--meal picker--|                      |               |
 |                |                      |               |
 |--[Pasta]------>|  (week:assign:1:...:id)             |
 |                |--UpsertAsync-------->|               |
 |                |--GetWeekAsync------->|               |
 |                |<--plan---------------|               |
 |                |--GetAllAsync---------|-------------> |
 |                |<--meals--------------|<------------- |
 |                |--EditMessage-------->|               |
 |<--week view----|                      |               |
```

---

## Localization Keys

All keys added to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`:

| Key | English value |
|---|---|
| `weekplan.header` | `📅 Meal plan for this week:` |
| `weekplan.pick-prompt` | `Choose a meal for {0}:` |
| `weekplan.no-meals` | `No meals in your library yet. Add meals with /meals first.` |
| `weekplan.btn-set` | `＋ Set` |
| `weekplan.btn-clear` | `✕` |
| `weekplan.btn-back` | `← Back` |
| `weekplan.day.1` | `Mon` |
| `weekplan.day.2` | `Tue` |
| `weekplan.day.3` | `Wed` |
| `weekplan.day.4` | `Thu` |
| `weekplan.day.5` | `Fri` |
| `weekplan.day.6` | `Sat` |
| `weekplan.day.7` | `Sun` |

---

## AOT Compatibility Notes

- No reflection is used. `WeekMealPlanRepository` uses raw `SqliteCommand` with parameterized queries.
- `WeekPlanCallbackHandler` parses callback data with `string.Split` and `int.Parse`; no `JsonSerializer` needed (all data is embedded in callback strings).
- No new types need to be registered in `BotActionPayloadContext`.
