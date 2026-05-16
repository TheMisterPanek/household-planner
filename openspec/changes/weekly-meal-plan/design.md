## Schema

### New table: `DayMeals`

Added in `DatabaseInitializer.StartAsync` using `CREATE TABLE IF NOT EXISTS`:

```sql
CREATE TABLE IF NOT EXISTS DayMeals (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    GroupId   INTEGER NOT NULL REFERENCES Groups(Id),
    DayOfWeek INTEGER NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),  -- 1=Mon, 7=Sun
    MealId    INTEGER NOT NULL REFERENCES Meals(Id),
    UNIQUE(GroupId, DayOfWeek)
);
```

No date column. The plan is abstract (recurring Mon–Sun). `UpsertAsync` uses `INSERT OR REPLACE` to enforce the unique constraint.

---

## Model: `DayMealEntry`

```csharp
// ProductTrackerBot/Models/DayMealEntry.cs
public record DayMealEntry
{
    public int DayOfWeek { get; init; }   // 1=Mon … 7=Sun
    public int MealId    { get; init; }
    public string MealName { get; init; } = string.Empty;
}
```

---

## Repository: `DayMealsRepository`

```
GetWeekAsync(int groupId) → Task<List<DayMealEntry>>
    SELECT dm.DayOfWeek, dm.MealId, m.Name
    FROM DayMeals dm JOIN Meals m ON dm.MealId = m.Id
    WHERE dm.GroupId = @groupId
    ORDER BY dm.DayOfWeek

UpsertAsync(int groupId, int dayOfWeek, int mealId) → Task
    INSERT OR REPLACE INTO DayMeals (GroupId, DayOfWeek, MealId)
    VALUES (@groupId, @dayOfWeek, @mealId)

ClearAsync(int groupId, int dayOfWeek) → Task
    DELETE FROM DayMeals WHERE GroupId = @groupId AND DayOfWeek = @dayOfWeek
```

All methods `virtual` for mockability. Registered as `builder.Services.AddScoped<DayMealsRepository>()`.

---

## Callback Data Format

All callbacks use the `week:` prefix. All fit well within the 64-byte Telegram limit.

| Callback data | Example | Bytes | Meaning |
|---|---|---|---|
| `week:pick:{day}` | `week:pick:1` | 12 | Open meal picker for that day |
| `week:assign:{day}:{mealId}` | `week:assign:3:42` | 17 | Assign meal to that day |
| `week:clear:{day}` | `week:clear:7` | 13 | Remove assignment for that day |
| `week:back` | `week:back` | 9 | Return to week view |
| `week:noop` | `week:noop` | 9 | No-op for display-only buttons |

`{day}` is 1–7 (1=Monday). No date token needed.

---

## Handler: `WeekCommandHandler`

`Command = "/week"`, `Description = "View and plan meals for the week"`

```
HandleAsync(message):
  if not group chat → reply localizer.Get(chatId, "common.group-only") and return
  group = groupRepository.GetOrCreateAsync(chatId)
  plan  = dayMealsRepository.GetWeekAsync(group.Id)   → dict DayOfWeek → DayMealEntry
  meals = mealRepository.GetAllAsync(group.Id)
  keyboard = BuildWeekKeyboard(plan, hasMeals: meals.Count > 0, localizer, chatId)
  SendMessage(localizer.Get(chatId, "week.header"), replyMarkup: keyboard)
```

### `BuildWeekKeyboard` logic

One row per day (1–7). Each row has:

- **Day has a meal**: `[{DayName}: {MealName}]` (noop) + `[✕]` (`week:clear:{day}`)
- **Day unset, group has meals**: `[{DayName}: —]` (noop) + `[＋ Set]` (`week:pick:{day}`)
- **Day unset, no meals in library**: `[{DayName}: —]` (noop only, no Set button)

Day names resolved via `localizer.Get(chatId, "week.day.{n}")` (1=Mon … 7=Sun).

---

## Handler: `WeekCallbackHandler`

`CallbackPrefix = "week:"`

```
HandleAsync(callbackQuery):
  parts  = data.Split(':')
  action = parts[1]   // "pick" | "assign" | "clear" | "back" | "noop"
  chatId = callbackQuery.Message!.Chat.Id
  group  = groupRepository.GetOrCreateAsync(chatId)

  switch action:
    "pick"   → HandlePickAsync(callbackQuery, group.Id, int.Parse(parts[2]))
    "assign" → HandleAssignAsync(callbackQuery, group.Id, int.Parse(parts[2]), int.Parse(parts[3]))
    "clear"  → HandleClearAsync(callbackQuery, group.Id, int.Parse(parts[2]))
    "back"   → RenderWeekViewAsync(callbackQuery.Message, group.Id)
    "noop"   → (answer only)
  AnswerCallbackQuery()
```

### `HandlePickAsync(callbackQuery, groupId, dayOfWeek)`

```
meals = mealRepository.GetAllAsync(groupId)
if meals.Count == 0:
  AnswerCallbackQuery(localizer.Get(chatId, "week.no-meals"))
  return
keyboard = meals rows: [InlineKeyboardButton(m.Name, $"week:assign:{dayOfWeek}:{m.Id}")]
         + [[localizer.Get("week.btn-back"), "week:back"]]
dayName  = localizer.Get(chatId, $"week.day.{dayOfWeek}")
EditMessageText(localizer.Get(chatId, "week.pick-prompt", dayName), keyboard)
```

### `HandleAssignAsync(callbackQuery, groupId, dayOfWeek, mealId)`

```
dayMealsRepository.UpsertAsync(groupId, dayOfWeek, mealId)
RenderWeekViewAsync(callbackQuery.Message, groupId)
```

### `HandleClearAsync(callbackQuery, groupId, dayOfWeek)`

```
dayMealsRepository.ClearAsync(groupId, dayOfWeek)
RenderWeekViewAsync(callbackQuery.Message, groupId)
```

### `RenderWeekViewAsync(message, groupId)` (private async helper)

```
plan  = dayMealsRepository.GetWeekAsync(groupId)
meals = mealRepository.GetAllAsync(groupId)
keyboard = BuildWeekKeyboard(plan, hasMeals: meals.Count > 0, localizer, chatId)
EditMessageText(chatId, message.MessageId, localizer.Get(chatId, "week.header"), keyboard)
```

---

## Sequence Diagram: Assign a meal to a day

```
User         Bot              DayMealsRepo   MealRepo
 |               |                  |             |
 |--/week------> |                  |             |
 |               |--GetWeekAsync--> |             |
 |               |<--plan-----------|             |
 |               |--GetAllAsync-----|-----------> |
 |               |<--meals----------|<----------- |
 |               |--SendMessage                   |
 |<--week view---|                  |             |
 |               |                  |             |
 |--[＋ Set Mon] |  (week:pick:1)  |             |
 |               |--GetAllAsync-----|-----------> |
 |               |<--meals----------|<----------- |
 |               |--EditMessage                   |
 |<--meal picker |                  |             |
 |               |                  |             |
 |--[Pasta]----> | (week:assign:1:42)             |
 |               |--UpsertAsync---> |             |
 |               |--GetWeekAsync--> |             |
 |               |<--plan-----------|             |
 |               |--GetAllAsync-----|-----------> |
 |               |<--meals----------|<----------- |
 |               |--EditMessage                   |
 |<--week view---|                  |             |
```

---

## Localization Keys

Added to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`:

| Key | English value |
|---|---|
| `week.header` | `📅 Meal plan:` |
| `week.pick-prompt` | `Choose a meal for {0}:` |
| `week.no-meals` | `No meals in your library yet. Add meals with /meals first.` |
| `week.btn-set` | `＋ Set` |
| `week.btn-clear` | `✕` |
| `week.btn-back` | `← Back` |
| `week.day.1` | `Mon` |
| `week.day.2` | `Tue` |
| `week.day.3` | `Wed` |
| `week.day.4` | `Thu` |
| `week.day.5` | `Fri` |
| `week.day.6` | `Sat` |
| `week.day.7` | `Sun` |

---

## AOT Compatibility Notes

- No reflection used. `DayMealsRepository` uses raw `SqliteCommand` with parameterized queries.
- `WeekCallbackHandler` parses callback data with `string.Split` and `int.Parse`; no `JsonSerializer` needed.
- No new types need to be registered in `BotActionPayloadContext`.
