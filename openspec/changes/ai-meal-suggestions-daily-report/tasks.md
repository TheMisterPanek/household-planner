## 1. `MealSuggestionItem` model + JSON context

- [ ] 1.1 Create `Models/MealSuggestionItem.cs`:
  ```csharp
  public record MealSuggestionItem(
      [property: JsonPropertyName("meal_id")] int MealId,
      [property: JsonPropertyName("meal_name")] string MealName,
      [property: JsonPropertyName("uses")] List<string> Uses,
      [property: JsonPropertyName("need_to_buy")] List<string> NeedToBuy);
  ```
- [ ] 1.2 Create `Models/AiMealSuggestionJsonContext.cs` with `[JsonSerializable(typeof(List<MealSuggestionItem>))]` partial class extending `JsonSerializerContext`.
- [ ] 1.3 Run `dotnet build` — confirm 0 errors.

---

## 2. `AiMealSuggestionService`

- [ ] 2.1 Create `Services/AiMealSuggestionService.cs`. Constructor: `IOpenRouterClient`, `MealRepository`, `MealIngredientRepository`, `AiQueryOptions`, `ILogger<AiMealSuggestionService>`.
- [ ] 2.2 Implement `SuggestAsync(int groupId, IReadOnlyList<(string Name, string? Quantity, DateOnly ExpDate)> expiringItems, CancellationToken ct)`:
  - Guard: `options.ApiKey` empty → return `[]`.
  - Guard: `expiringItems` empty or no meals → return `[]`.
  - Load all meals for group; for each meal load its ingredients.
  - Build user message string: `"Expiring items: item1, item2\nMeal library:\n- Name (id:N): ing1, ing2"`.
  - System prompt asks for JSON-only response with up to 3 suggestions.
  - Call `openRouterClient.CompleteAsync(options.Model, systemPrompt, userMessage, ct)`.
  - Deserialize with `JsonSerializer.Deserialize<List<MealSuggestionItem>>(json, AiMealSuggestionJsonContext.Default.ListMealSuggestionItem)`.
  - On `HttpRequestException`, `TaskCanceledException`, `JsonException`, or null response → log Warning, return `[]`.
  - Return parsed list, trimmed to at most 3 items.

---

## 3. Localization keys — `suggest.*`

- [ ] 3.1 Add to `Strings.en.json`:
  - `"suggest.section-header"`: `"🤖 What to cook with expiring items:"`
  - `"suggest.meal-uses"`: `"Uses: {items}"`
  - `"suggest.meal-need-to-buy"`: `"Still need: {items}"`
  - `"suggest.assign-today-btn"`: `"🍽 {meal} → Today"`
  - `"suggest.assigned-today"`: `"✓ {meal} assigned to today"`
- [ ] 3.2 Add to `Strings.ru.json`:
  - `"suggest.section-header"`: `"🤖 Что приготовить из продуктов с истекающим сроком:"`
  - `"suggest.meal-uses"`: `"Использует: {items}"`
  - `"suggest.meal-need-to-buy"`: `"Нужно купить: {items}"`
  - `"suggest.assign-today-btn"`: `"🍽 {meal} → Сегодня"`
  - `"suggest.assigned-today"`: `"✓ {meal} назначено на сегодня"`
- [ ] 3.3 Add to `Strings.pl.json`:
  - `"suggest.section-header"`: `"🤖 Co ugotować z wygasających produktów:"`
  - `"suggest.meal-uses"`: `"Używa: {items}"`
  - `"suggest.meal-need-to-buy"`: `"Trzeba kupić: {items}"`
  - `"suggest.assign-today-btn"`: `"🍽 {meal} → Dziś"`
  - `"suggest.assigned-today"`: `"✓ {meal} przypisano na dziś"`

---

## 4. `SuggestCallbackHandler`

- [ ] 4.1 Create `Handlers/SuggestCallbackHandler.cs` implementing `ICallbackHandler`:
  - `CallbackPrefix = "suggest:"`.
  - Constructor: `ITelegramBotClient`, `GroupRepository`, `DayMealsRepository`, `MealRepository`, `IHistoryRepository`, `ILocalizer`, `ILogger<SuggestCallbackHandler>`.
- [ ] 4.2 Handle `suggest:assign_today:{mealId}`:
  - Parse `mealId` from `parts[2]`; on failure → `AnswerCallbackQuery` silently and return.
  - If private chat → answer silently and return.
  - Resolve group: `group = await groupRepository.GetOrCreateAsync(chatId)`.
  - Compute `todayDow` (1=Mon, 7=Sun) and `weekStart` (this week's Monday, `yyyy-MM-dd`).
  - `await dayMealsRepository.ClearAsync(group.Id, todayDow, weekStart)`.
  - `await dayMealsRepository.InsertAsync(group.Id, todayDow, mealId, weekStart)`.
  - Fetch meal name: `var meal = await mealRepository.GetByIdAsync(mealId)`.
  - `try { await historyRepository.RecordAsync(...); } catch { logger.LogWarning(...); }`.
  - `await botClient.AnswerCallbackQuery(callbackQuery.Id, localizer.Get(chatId, "suggest.assigned-today").Replace("{meal}", meal?.Name ?? mealId.ToString()))`.

---

## 5. Extend `ExpiryNotificationJob`

- [ ] 5.1 Add `AiMealSuggestionService`, `ShoppingItemRepository`, and `PurchaseHistoryRepository` to the scoped services resolved inside `ExecuteNotificationAsync`.
- [ ] 5.2 Add private static helper `BuildExpiringList(plannedItems, boughtItems, today)` returning `IReadOnlyList<(string Name, string? Quantity, DateOnly ExpDate)>` — items expiring within 7 days of today (i.e., `ExpDate <= today.AddDays(7)`).
- [ ] 5.3 After `BuildSummaryAsync`, call `aiSuggestionService.SuggestAsync(group.Id, expiringList, ct)`.
- [ ] 5.4 Add private method `BuildSuggestionsText(long chatId, List<MealSuggestionItem> suggestions)` — builds the localized suggestions section string using `suggest.section-header`, `suggest.meal-uses`, `suggest.meal-need-to-buy`. Returns empty string if no suggestions.
- [ ] 5.5 Add private static method `BuildSuggestionKeyboard(long chatId, List<MealSuggestionItem> suggestions, ILocalizer localizer)` — returns `InlineKeyboardMarkup` with one row per suggestion using `suggest.assign-today-btn` label and `suggest:assign_today:{mealId}` callback data.
- [ ] 5.6 Combine `expiryText` and suggestions text into `combinedText`; skip sending only if both are empty.
- [ ] 5.7 Pass `replyMarkup: keyboard` (or `null` when no suggestions) to `botClient.SendMessage`.

---

## 6. DI Registration in `Program.cs`

- [ ] 6.1 Register `AiMealSuggestionService` as `AddScoped<AiMealSuggestionService>`.
- [ ] 6.2 Register `SuggestCallbackHandler` as `AddScoped<ICallbackHandler, SuggestCallbackHandler>`.
- [ ] 6.3 Run `dotnet build` — confirm 0 errors.

---

## 7. Unit tests — `AiMealSuggestionService`

- [ ] 7.1 Empty `ApiKey` → returns `[]` with no HTTP call.
- [ ] 7.2 Empty expiring items → returns `[]` with no HTTP call.
- [ ] 7.3 No meals in library → returns `[]` with no HTTP call.
- [ ] 7.4 Valid AI response (3 suggestions) → returns deserialized list, capped at 3 items.
- [ ] 7.5 AI returns more than 3 → list is trimmed to 3.
- [ ] 7.6 AI returns malformed JSON → logs Warning, returns `[]`.
- [ ] 7.7 `CompleteAsync` throws `HttpRequestException` → logs Warning, returns `[]`.
- [ ] 7.8 `CompleteAsync` returns `null` → returns `[]`.
- [ ] 7.9 Prompt contains each expiring item name and each meal name with its ingredients.

---

## 8. Unit tests — `SuggestCallbackHandler`

- [ ] 8.1 `suggest:assign_today:5` in group chat → `ClearAsync` and `InsertAsync` called with correct `groupId`, `todayDow`, `weekStart`; `AnswerCallbackQuery` called with confirmation.
- [ ] 8.2 `suggest:assign_today:5` in private chat → no DB calls; `AnswerCallbackQuery` called silently.
- [ ] 8.3 `suggest:assign_today:abc` (unparseable mealId) → no DB calls; `AnswerCallbackQuery` called.
- [ ] 8.4 Meal not found in DB (`GetByIdAsync` returns null) → still calls `InsertAsync`; confirmation uses mealId as fallback name.
- [ ] 8.5 `IHistoryRepository.RecordAsync` throws → Warning logged; no crash; confirmation still sent.

---

## 9. Unit tests — `ExpiryNotificationJob` extension

- [ ] 9.1 When `AiMealSuggestionService` returns non-empty list → `SendMessage` is called with `InlineKeyboardMarkup` containing the correct callbacks.
- [ ] 9.2 When `AiMealSuggestionService` returns `[]` → `SendMessage` is called without inline keyboard (null markup).
- [ ] 9.3 When expiry summary is null and AI suggestions are empty → `SendMessage` is not called.
- [ ] 9.4 When expiry summary is null but AI returns suggestions → `SendMessage` is called (suggestions-only message).
- [ ] 9.5 `BuildExpiringList` includes items expiring within 7 days and excludes items expiring after 7 days and items with no expiry.

---

## 10. Integration test — `suggest:assign_today` flow

- [ ] 10.1 Seed a meal; simulate `suggest:assign_today:{mealId}` callback in a group → `DayMeals` table has a row for today's day-of-week with the correct `MealId`; bot answers callback query.
- [ ] 10.2 Simulate callback twice for the same meal → only one row in `DayMeals` for today (clear + insert pattern).
- [ ] 10.3 Simulate callback in private chat → no `DayMeals` row inserted; callback answered.

---

## 11. Landing page update

- [ ] 11.1 Update `ProductTrackerBot.Web/Components/Pages/Dashboard.razor` (or equivalent landing page) to mention the new AI meal suggestions in the daily report and the one-tap "Assign to Today" feature.

---

## 12. Smoke test (manual e2e — do NOT mark complete until confirmed by user)

Perform the following in a Telegram group chat where the bot is active and the environment has a valid `OPENROUTER_API_KEY`:

1. Register at least 2 items with expiry dates within the next 3 days using `/bought`:
   - `/bought tomatoes` → set expiry to 2 days
   - `/bought eggs` → set expiry to 1 day
2. Add at least one meal with ingredients in `/meals` that uses "tomatoes" or "eggs".
3. Trigger the daily notification manually (set `NOTIFY_INTERVAL_MINUTES=1` and wait, or restart bot with short interval).
4. Verify the notification message contains:
   - The standard expiry section (items listed by bucket).
   - A `🤖 What to cook with expiring items:` section listing the suggested meal(s).
   - Each suggestion shows `Uses:` and optionally `Still need:`.
   - Inline buttons at the bottom: `🍽 [MealName] → Today` for each suggestion.
5. Tap one of the **[→ Today]** buttons → bot answers with `✓ [meal] assigned to today`.
6. Send `/week` → verify the meal appears in today's slot.
7. Tap the same button again → meal is re-assigned (no duplicate rows); `/week` still shows one entry for today.
8. Try the flow without any expiring items (all items have expiry > 7 days) → notification sent (if any expired/soon items exist) but **no** AI section appears.
9. Try with `OPENROUTER_API_KEY` unset → notification sent normally, no AI section, no error message to the chat.
