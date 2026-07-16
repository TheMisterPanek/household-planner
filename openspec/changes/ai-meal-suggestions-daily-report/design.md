# Design: AI Meal Suggestions in Daily Expiry Report + `/week` Today Assignment

## Overview

This change extends the existing daily expiry job with an AI meal suggestion section and a one-tap "Assign to Today" inline button per suggestion. No schema changes are required.

---

## Data Flow

```
ExpiryNotificationJob.ExecuteNotificationAsync
  │
  ├─ [existing] ExpiryNotificationService.BuildSummaryAsync
  │     └─ returns: expiryText (string | null)
  │
  ├─ [new] AiMealSuggestionService.SuggestAsync(groupId, expiringItems)
  │     ├─ MealRepository.GetAllAsync(groupId)
  │     ├─ For each meal: MealIngredientRepository.GetAllAsync(mealId)
  │     ├─ IOpenRouterClient.CompleteAsync(model, systemPrompt, userMessage)
  │     └─ returns: List<MealSuggestionItem> (up to 3, or [] on failure/skip)
  │
  └─ botClient.SendMessage(chatId, combinedText, replyMarkup: inlineKeyboard)
        └─ inlineKeyboard: one row per suggestion → [Emoji MealName → Assign to Today]
                           callback: suggest:assign_today:{mealId}
```

### Sequence Diagram

```
ExpiryNotificationJob          AiMealSuggestionService     IOpenRouterClient     DayMealsRepository
        |                               |                          |                      |
        |--BuildSummaryAsync()--------->|                          |                      |
        |<--expiryText (or null)--------|                          |                      |
        |                               |                          |                      |
        |--SuggestAsync(groupId, items)->|                          |                      |
        |                               |--GetAllAsync(groupId)--->|                      |
        |                               |<--meals list-------------|                      |
        |                               |--GetAllAsync(mealId) x N>|                      |
        |                               |<--ingredients x N--------|                      |
        |                               |--CompleteAsync(prompt)------------------>|      |
        |                               |<--JSON string----------------------------|      |
        |                               |--parse JSON                              |      |
        |<--List<MealSuggestionItem>----|                          |                      |
        |                               |                          |                      |
        |--botClient.SendMessage(text + suggestions + keyboard)--->|                      |
        |                               |                          |                      |

On callback suggest:assign_today:5 (user taps button):
SuggestCallbackHandler
        |--GroupRepository.GetOrCreateAsync(chatId)
        |--DayMealsRepository.ClearAsync(groupId, todayDow, weekStart)
        |--DayMealsRepository.InsertAsync(groupId, todayDow, mealId, weekStart)
        |--IHistoryRepository.RecordAsync(...)  ← try/catch
        |--botClient.AnswerCallbackQuery(confirmationText)
```

---

## New Models

### `MealSuggestionItem`

```csharp
// Models/MealSuggestionItem.cs
public record MealSuggestionItem(
    [property: JsonPropertyName("meal_id")] int MealId,
    [property: JsonPropertyName("meal_name")] string MealName,
    [property: JsonPropertyName("uses")] List<string> Uses,
    [property: JsonPropertyName("need_to_buy")] List<string> NeedToBuy);
```

### `AiMealSuggestionJsonContext`

```csharp
// Models/AiMealSuggestionJsonContext.cs
[JsonSerializable(typeof(List<MealSuggestionItem>))]
public partial class AiMealSuggestionJsonContext : JsonSerializerContext { }
```

---

## `AiMealSuggestionService`

**Constructor parameters**: `IOpenRouterClient openRouterClient`, `MealRepository mealRepository`, `MealIngredientRepository mealIngredientRepository`, `AiQueryOptions options`, `ILogger<AiMealSuggestionService> logger`

**Guard**: if `options.ApiKey` is `null` or empty → return `[]` immediately (no call made).

**`SuggestAsync(int groupId, IReadOnlyList<(string Name, string? Quantity, DateOnly ExpDate)> expiringItems, CancellationToken ct)`**

1. If `expiringItems` is empty → return `[]`.
2. Load all meals for group. If empty → return `[]`.
3. For each meal, load its ingredients (parallel or sequential — sequential is fine for small libraries).
4. Build user message:

```
Expiring items: tomatoes, milk 1L, eggs
Meal library:
- Pasta (id:3): pasta, tomatoes, garlic, olive oil
- Omelette (id:7): eggs, milk, cheese, butter
- Greek Salad (id:11): tomatoes, cucumber, feta, olives
```

5. System prompt:

```
You are a meal planning assistant. The household has food that is expiring soon.
From the provided meal library, suggest up to 3 meals that use at least one expiring item.
For each suggestion, list which expiring items it uses and what additional ingredients still need to be purchased.
Respond ONLY with a JSON array (no markdown, no prose):
[{"meal_id":N,"meal_name":"...","uses":["item1"],"need_to_buy":["item2"]}]
If no meals match, respond with an empty array: []
```

6. Call `IOpenRouterClient.CompleteAsync(options.Model, systemPrompt, userMessage, ct)`.
7. Parse response with `JsonSerializer.Deserialize<List<MealSuggestionItem>>(json, AiMealSuggestionJsonContext.Default.ListMealSuggestionItem)`.
8. On `HttpRequestException`, `TaskCanceledException`, or `JsonException` → log Warning, return `[]`.
9. Return parsed list (at most 3 items; trim if AI returns more).

---

## Modified `ExpiryNotificationJob.ExecuteNotificationAsync`

```csharp
// Inside the per-group try block:
var expiryText = await notificationService.BuildSummaryAsync(group.ChatId, group.Id, today);

// NEW: get expiring items for AI (reuse same data already computed in BuildSummaryAsync is not
// directly accessible, so re-fetch here — two extra DB calls per group, acceptable)
var plannedItems = await itemRepository.GetItemsWithExpiryAsync(group.Id);
var boughtItems = await purchaseRepository.GetItemsWithExpiryAsync(group.Id);
var expiringItems = BuildExpiringList(plannedItems, boughtItems, today); // helper, returns items expiring within 7 days

var suggestions = await aiSuggestionService.SuggestAsync(group.Id, expiringItems, cancellationToken);

var messageText = BuildCombinedMessage(group.ChatId, expiryText, suggestions);
if (messageText is null) continue; // nothing to report

InlineKeyboardMarkup? keyboard = null;
if (suggestions.Count > 0)
{
    keyboard = BuildSuggestionKeyboard(suggestions);
}

await botClient.SendMessage(chatId: group.ChatId, text: messageText,
    replyMarkup: keyboard, cancellationToken: cancellationToken);
```

`BuildExpiringList` — private static helper that merges planned and bought items and returns only those expiring within 7 days of today (same window as `BuildSummaryAsync`'s widest bucket). Items with no expiry date are excluded.

`BuildCombinedMessage` — appends the AI suggestions section after the expiry text. Returns `null` only if both sections are empty.

`BuildSuggestionKeyboard` — creates an `InlineKeyboardMarkup` with one row per suggestion: `suggest:assign_today:{mealId}`.

---

## `SuggestCallbackHandler`

```
CallbackPrefix = "suggest:"
```

Handles `suggest:assign_today:{mealId}`:

1. Parse `mealId` from `parts[2]`. On failure → `AnswerCallbackQuery` silently.
2. Check group context: if `callbackQuery.Message.Chat.Type == ChatType.Private` → answer silently.
3. `group = await groupRepository.GetOrCreateAsync(chatId)`.
4. Calculate `todayDow` and `weekStart`:
   ```csharp
   var nowDow = (int)DateTime.UtcNow.DayOfWeek; // 0=Sun
   var todayDow = nowDow == 0 ? 7 : nowDow;    // remap to 1=Mon,7=Sun
   var weekStart = DateOnly.FromDateTime(DateTime.UtcNow)
       .AddDays(-(todayDow - 1))
       .ToString("yyyy-MM-dd");
   ```
5. `await dayMealsRepository.ClearAsync(group.Id, todayDow, weekStart)`.
6. `await dayMealsRepository.InsertAsync(group.Id, todayDow, mealId, weekStart)`.
7. Fetch meal name: `var meal = await mealRepository.GetByIdAsync(mealId)`.
8. `try { await historyRepository.RecordAsync(...); } catch { logger.LogWarning(...); }`.
9. `await botClient.AnswerCallbackQuery(callbackQuery.Id, localizer.Get(chatId, "suggest.assigned-today").Replace("{meal}", meal?.Name ?? mealId.ToString()))`.

---

## Localization Keys

### `suggest.*`

| Key | EN | RU | PL |
|---|---|---|---|
| `suggest.section-header` | `🤖 What to cook with expiring items:` | `🤖 Что приготовить из продуктов с истекающим сроком:` | `🤖 Co ugotować z wygasających produktów:` |
| `suggest.meal-uses` | `Uses: {items}` | `Использует: {items}` | `Używa: {items}` |
| `suggest.meal-need-to-buy` | `Still need: {items}` | `Нужно купить: {items}` | `Trzeba kupić: {items}` |
| `suggest.assign-today-btn` | `🍽 {meal} → Today` | `🍽 {meal} → Сегодня` | `🍽 {meal} → Dziś` |
| `suggest.assigned-today` | `✓ {meal} assigned to today` | `✓ {meal} назначено на сегодня` | `✓ {meal} przypisano na dziś` |
| `suggest.no-suggestions` | *(omitted — section is simply absent when no suggestions)* | | |

---

## Callback Data Constraint Check

- Longest callback: `suggest:assign_today:99999` = 25 bytes ✓ (< 64 bytes)

---

## AOT Compatibility

- `MealSuggestionItem` is a positional record with `[JsonPropertyName]` — source-gen serializable.
- `AiMealSuggestionJsonContext` uses `[JsonSerializable(typeof(List<MealSuggestionItem>))]` — no reflection.
- No new NuGet packages.
- No `dynamic`, no `Activator.CreateInstance`, no reflection.

---

## Schema Impact

None. This change reads from existing tables only.

---

## Error Handling

| Scenario | Behaviour |
|---|---|
| `ApiKey` empty | Skip AI section; send expiry-only report (or nothing if also empty) |
| AI call timeout / HTTP error | Log Warning; skip AI section; continue with expiry-only report |
| AI returns malformed JSON | Log Warning; skip AI section |
| AI returns empty `[]` | Section omitted (no header rendered) |
| `assign_today` callback, meal not found | Answer with generic confirmation; no crash |
| `assign_today` in private chat | Answer silently |
