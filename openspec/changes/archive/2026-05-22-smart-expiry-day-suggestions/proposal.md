# Proposal: Smart Expiry Day Suggestions

## What & Why

When the bot asks "how many days until expiry?" users have to recall or guess a number from memory. For recurring items like milk, yogurt, or bread, the bot already knows how long those items lasted in the past — it just doesn't surface that knowledge. This friction causes users to skip the expiry step or enter arbitrary values, degrading the accuracy of expiry notifications.

This change adds **smart suggestion buttons** to every expiry-date prompt. When the system asks how many days, it:
1. Looks up past purchases of similar items (fuzzy name match on `PurchaseHistory.exp_date`) and derives how long each one lasted.
2. Presents the most common durations as one-tap inline buttons (up to 3 distinct values).
3. Falls back to the group-wide average days-to-expiry when there is not enough item-specific data.
4. Shows no suggestion buttons (only Skip) when the group has no expiry history at all.

This applies to all two expiry-date prompts in the bot:
- `/bought` command dialog (step 2 — `BoughtStepHandler`)
- Price-capture dialog after marking a shopping item done (step 3 — `PriceCaptureStepHandler`)

## Proposed Solution

### New: `ExpiryDaySuggestionService`

Stateless, scoped service. Given a `groupId` and `itemName`, it:
1. Calls `PurchaseHistoryRepository.GetExpiryDaySuggestionsAsync(groupId, itemName)` — a new SQL query that computes `julianday(exp_date) - julianday(date(PurchasedAt))` for rows where both fields are present and `ItemName` is similar (case-insensitive `LIKE '%keyword%'`). Returns distinct day counts sorted by frequency descending, limited to 5.
2. If `count(distinct days) >= 1` → uses those values (up to 3 most frequent).
3. Otherwise calls `PurchaseHistoryRepository.GetAverageExpiryDaysAsync(groupId)` → returns the group-wide average days-to-expiry rounded to nearest integer.
4. Returns a `IReadOnlyList<int>` of suggestion values. Returns empty list when no history exists.

### Callback: `expiry:suggest:{days}`

A new `ExpirySuggestCallbackHandler` (prefix `expiry:`) handles `suggest:{days}`:
- Parses `days` and converts to a future `DateOnly` via `ExpiryInputParser`-equivalent logic.
- Checks which dialog is active for the user:
  - `PendingDialogService<BoughtDialogState>` at step 2 → delegates to `BoughtStepHandler.FinishDialogAsync`.
  - `PendingDialogService<PriceCaptureDialogState>` at step 3 → delegates to `PriceCaptureStepHandler.FinishDialogAsync` (made public).
- If neither dialog is active, answers the callback silently (stale button tap).

### Prompt Changes

Both `BoughtStepHandler.HandleStep1Async` and `PriceCaptureStepHandler.HandleStep2Async` call `ExpiryDaySuggestionService.GetSuggestionsAsync` before sending the expiry prompt. The inline keyboard becomes:

```
[7 days]  [14 days]  [30 days]  [Skip]
```

Suggestion buttons appear only when the service returns values. The Skip button is always present.

## Affected Components

| Component | Change |
|---|---|
| `ExpiryDaySuggestionService` (new) | Fetches similar-item and group-average day suggestions |
| `ExpirySuggestCallbackHandler` (new) | Handles `expiry:suggest:{days}` by advancing the active dialog |
| `PurchaseHistoryRepository` | Two new query methods: `GetExpiryDaySuggestionsAsync`, `GetAverageExpiryDaysAsync` |
| `BoughtStepHandler` | Calls suggestion service; adds suggestion buttons to expiry prompt |
| `PriceCaptureStepHandler` | Calls suggestion service; adds suggestion buttons to expiry prompt; `FinishDialogAsync` made public |
| `Strings.*.json` (en/ru/pl) | New key `expiry.suggest-days` for button label (`{n} days` / `{n} дн.` / `{n} dni`) |
| `Program.cs` | Register `ExpiryDaySuggestionService` and `ExpirySuggestCallbackHandler` |

## Rollback Plan

1. Remove `ExpiryDaySuggestionService.cs` and `ExpirySuggestCallbackHandler.cs`.
2. Revert `BoughtStepHandler` and `PriceCaptureStepHandler` to their previous expiry prompts (no suggestion buttons).
3. Revert `PriceCaptureStepHandler.FinishDialogAsync` back to `private`.
4. Remove the two new `PurchaseHistoryRepository` methods.
5. Remove `AddScoped` registrations from `Program.cs`.
6. Remove `expiry.suggest-days` key from `Strings.*.json`.

No schema changes → nothing to roll back in the DB.

## Affected Teams

Single-user/household bot. No shared infrastructure changes.

## Cross-Cutting Notes

- No new SQL tables or columns. Queries compute shelf-life inline from existing `PurchasedAt` and `exp_date` columns on `PurchaseHistory`.
- Callback data `expiry:suggest:365` = 17 bytes — well within Telegram's 64-byte limit.
- `ExpiryDaySuggestionService` is `AddScoped` (stateless per request).
- `ExpirySuggestCallbackHandler` resolves the active dialog at callback time; stale taps (no active dialog) are silently ignored via `AnswerCallbackQuery`.
- All user-facing strings (button labels) flow through `ILocalizer`. The suggestion count `{n}` is substituted at runtime.
- No `IHistoryRepository.RecordAsync` call needed: the delegated `FinishDialogAsync` methods already record history.
- AOT-safe: all new code uses constructor injection, static SQL, and no reflection.
