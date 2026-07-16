# Proposal: AI Meal Suggestions in Daily Expiry Report + `/week` Today Assignment

## What & Why

The daily expiry report tells the group *what is expiring*, but leaves the "so what do we cook?" question unanswered. Members still have to manually look up meals in `/meals`, check which ones use the expiring ingredients, figure out what extras to buy, and then open `/week` to assign for today. That's four friction points after receiving the notification.

This change:
1. **Extends the daily expiry notification** to include an AI-generated section: *"Here are meals you can cook from expiring items, and what you'd still need to buy."* Up to 3 meal suggestions are shown, each with a one-tap **[Assign to Today]** inline button.
2. **Adds the `suggest:assign_today` callback** so a single tap assigns the chosen meal to today's slot in the week plan — no need to open `/week` at all.

## Proposed Solution

### AI Meal Suggestions Section

`AiMealSuggestionService` (new) is called by `ExpiryNotificationJob` after the expiry summary is built. It receives:
- The group's expiring + expiring-soon items (same list already computed by `ExpiryNotificationService`)
- The group's full meal library with ingredients (from `MealRepository` + `MealIngredientRepository`)

It sends a single-round prompt to `IOpenRouterClient` asking for up to 3 meal suggestions from the library that use one or more expiring items. The response is expected as a JSON array of `MealSuggestionItem` records.

If the API key is missing, the AI section is silently skipped. If the AI call fails, a Warning is logged and the report is sent without the suggestions section.

### ASSIGN_TODAY_MEAL Button

Each suggested meal renders an inline button in the notification message:

```
[🍳 Pasta → Assign to Today]   callback: suggest:assign_today:5
[🥗 Salad → Assign to Today]   callback: suggest:assign_today:12
```

`SuggestCallbackHandler` (new, prefix `suggest:`) handles `assign_today:{mealId}`:
- Resolves today's ISO day-of-week (1=Mon, 7=Sun) and this week's Monday date
- Calls `DayMealsRepository.ClearAsync` + `InsertAsync` (replace existing today slot)
- Answers the callback query with a confirmation toast

## Affected Components

| Component | Change |
|---|---|
| `MealSuggestionItem` (new) | AOT-safe JSON record: `MealId`, `MealName`, `Uses`, `NeedToBuy` |
| `AiMealSuggestionJsonContext` (new) | Source-generated `JsonSerializerContext` for `List<MealSuggestionItem>` |
| `AiMealSuggestionService` (new) | Calls `IOpenRouterClient`; parses JSON response |
| `SuggestCallbackHandler` (new) | `suggest:assign_today:{mealId}` — upserts today's day slot |
| `ExpiryNotificationJob` | Calls `AiMealSuggestionService`; attaches inline keyboard to notification message |
| `Strings.*.json` (en/ru/pl) | New `suggest.*` localization keys |
| `Program.cs` | Register new service and callback handler |

## Rollback Plan

1. Remove `AiMealSuggestionService.cs`, `SuggestCallbackHandler.cs`, `MealSuggestionItem.cs`, `AiMealSuggestionJsonContext.cs`.
2. Revert `ExpiryNotificationJob.ExecuteNotificationAsync` to the previous single-service call (no AI section, no inline keyboard).
3. Remove the `AddScoped` registrations from `Program.cs`.
4. Remove `suggest.*` keys from `Strings.*.json`.

No schema changes → nothing to roll back in the DB.

## Affected Teams

Single-user/household bot. No shared infrastructure changes.

## Cross-Cutting Notes

- Uses the existing `IOpenRouterClient` — no new HTTP client, no new packages.
- AI key absent → graceful skip; AI call throws → log Warning, continue batch. No user-visible error for a missing suggestion section.
- `SuggestCallbackHandler` is group-only: private-chat invocations answer the callback silently.
- `DayMeals` upsert reuses existing `ClearAsync` + `InsertAsync` (same pattern as `WeekCallbackHandler`).
- Callback data `suggest:assign_today:9999` = 24 bytes — well within Telegram's 64-byte limit.
- All user-facing strings flow through `ILocalizer`.
- `AiMealSuggestionService` is `AddScoped` (stateless, short-lived scope per job tick).
- No `IHistoryRepository.RecordAsync` call for the AI suggestion section itself; the `assign_today` callback records history after successful insert.
- JSON deserialization of the AI response uses `AiMealSuggestionJsonContext` (source-generated; AOT-safe). The AI response is wrapped in a `try/catch`; malformed JSON returns an empty list.
