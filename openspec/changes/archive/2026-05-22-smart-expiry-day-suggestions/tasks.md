# Tasks: Smart Expiry Day Suggestions

## Implementation

- [x] **Add `GetExpiryDaySuggestionsAsync` to `PurchaseHistoryRepository`**
  Add a method that queries `PurchaseHistory` for records where `ItemName LIKE '%{keyword}%'` (case-insensitive) and `exp_date IS NOT NULL`, computes `ROUND(julianday(exp_date) - julianday(date(PurchasedAt)))` as the shelf-life days, groups by that value, orders by frequency descending, and returns up to 5 distinct `int` day counts. File: `ProductTrackerBot/Repositories/PurchaseHistoryRepository.cs`.

- [x] **Add `GetAverageExpiryDaysAsync` to `PurchaseHistoryRepository`**
  Add a method that returns the group-wide `CAST(ROUND(AVG(...)) AS INT)` shelf-life days from `PurchaseHistory` where `GroupId = @groupId AND exp_date IS NOT NULL`. Returns `0` if no rows match. File: `ProductTrackerBot/Repositories/PurchaseHistoryRepository.cs`.

- [x] **Create `ExpiryDaySuggestionService`**
  New file `ProductTrackerBot/Services/ExpiryDaySuggestionService.cs`. Implements `GetSuggestionsAsync(int groupId, string itemName) → Task<IReadOnlyList<int>>`:
  - Extracts the first word of `itemName` as the keyword (lower-cased).
  - Calls `GetExpiryDaySuggestionsAsync`; if ≥ 1 result, returns up to 3 values.
  - Otherwise calls `GetAverageExpiryDaysAsync`; if > 0, returns `[avg]`.
  - Otherwise returns `Array.Empty<int>()`.

- [x] **Create `ExpirySuggestCallbackHandler`**
  New file `ProductTrackerBot/Handlers/ExpirySuggestCallbackHandler.cs`. `CallbackPrefix = "expiry"`. Handles `expiry:suggest:{days}`:
  - Parses `days` from callback data.
  - Computes `expDate = DateOnly.FromDateTime(DateTime.Now.AddDays(days))`.
  - Checks `PendingDialogService<BoughtDialogState>` for active state at step 2 → delegates to `BoughtStepHandler.FinishDialogAsync`.
  - Else checks `PendingDialogService<PriceCaptureDialogState>` for active state at step 3 → delegates to `PriceCaptureStepHandler.FinishDialogAsync`.
  - Else answers callback query silently.
  - Always calls `AnswerCallbackQuery` to dismiss the spinner.

- [x] **Make `PriceCaptureStepHandler.FinishDialogAsync` public**
  Change the access modifier from `private` to `public` so `ExpirySuggestCallbackHandler` can call it. File: `ProductTrackerBot/Handlers/PriceCaptureStepHandler.cs`.

- [x] **Update `BoughtStepHandler` to show suggestion buttons**
  Inject `ExpiryDaySuggestionService`. In `HandleStep1Async`, after setting state to step 2, call `GetSuggestionsAsync(state.GroupId, itemName)` and build suggestion `InlineKeyboardButton`s using callback data `expiry:suggest:{n}` and label from `expiry.suggest-days`. Add the Skip button last. File: `ProductTrackerBot/Handlers/BoughtStepHandler.cs`.

- [x] **Update `PriceCaptureStepHandler` to show suggestion buttons**
  Inject `ExpiryDaySuggestionService`. In `HandleStep2Async`, after advancing to step 3, call `GetSuggestionsAsync(group.Id, state.ItemName)` and build suggestion buttons the same way before sending the expiry prompt. File: `ProductTrackerBot/Handlers/PriceCaptureStepHandler.cs`.
  
  Note: `PriceCaptureStepHandler` currently uses the group's `Id` from `state`. You may need to store `GroupId` in `PriceCaptureDialogState` or call `GroupRepository.GetOrCreateAsync` early — whichever is simpler given the current state model.

- [x] **Add `expiry.suggest-days` localization key**
  Add to all three locale files:
  - `Strings.en.json`: `"expiry.suggest-days": "{n} days"`
  - `Strings.ru.json`: `"expiry.suggest-days": "{n} дн."`
  - `Strings.pl.json`: `"expiry.suggest-days": "{n} dni"`
  Files: `ProductTrackerBot/Localization/Strings.*.json`.

- [x] **Register new service and callback handler in `Program.cs`**
  Add:
  ```csharp
  builder.Services.AddScoped<ExpiryDaySuggestionService>();
  builder.Services.AddScoped<ICallbackHandler, ExpirySuggestCallbackHandler>();
  ```
  File: `ProductTrackerBot/Program.cs`.

## Tests

- [x] **Unit tests for `PurchaseHistoryRepository` new methods**
  File: `ProductTrackerBot.Tests/Repositories/PurchaseHistoryRepositoryExpiryTests.cs`.
  - `GetExpiryDaySuggestionsAsync` returns most-frequent days when similar items exist.
  - `GetExpiryDaySuggestionsAsync` returns empty when no matching item names.
  - `GetExpiryDaySuggestionsAsync` excludes rows with null `exp_date`.
  - `GetAverageExpiryDaysAsync` returns rounded average when data exists.
  - `GetAverageExpiryDaysAsync` returns 0 when no rows with both fields set.

- [x] **Unit tests for `ExpiryDaySuggestionService`**
  File: `ProductTrackerBot.Tests/Services/ExpiryDaySuggestionServiceTests.cs`.
  - Returns up to 3 suggestions when similar items found.
  - Returns fallback average when no similar items but group has history.
  - Returns empty list when group has no expiry history.
  - Uses the first word of item name as the keyword.

- [x] **Unit tests for `ExpirySuggestCallbackHandler`**
  File: `ProductTrackerBot.Tests/Handlers/ExpirySuggestCallbackHandlerTests.cs`.
  - Routes to `BoughtStepHandler.FinishDialogAsync` when `BoughtDialogState` is active at step 2.
  - Routes to `PriceCaptureStepHandler.FinishDialogAsync` when `PriceCaptureDialogState` is active at step 3.
  - Answers silently (no crash, calls `AnswerCallbackQuery`) when no dialog is active.
  - Parses days correctly from callback data `expiry:suggest:{n}`.

- [x] **Integration test: suggestion buttons appear in `/bought` expiry prompt**
  File: `ProductTrackerBot.Tests/Integration/SmartExpirySuggestionsIntegrationTests.cs`.
  - Seed a group with two prior purchases of "milk" (exp_date set to 7 days after purchase). 
  - Send `/bought` then `milk` text.
  - Assert the bot replies with a message whose `ReplyMarkup` contains a button with callback `expiry:suggest:7`.

- [x] **Integration test: tapping suggestion button completes the `/bought` dialog**
  - Continue from the previous test state.
  - Send callback `expiry:suggest:7`.
  - Assert `BoughtStepHandler.FinishDialogAsync` runs: bot sends confirmation message, `PurchaseHistory` row has `exp_date` set ~7 days from now.

- [x] **Integration test: fallback average shown when no similar item history**
  - Seed a group with purchases of unrelated items with exp_date set (e.g., 14 days on average).
  - Send `/bought` then `newitem` text.
  - Assert the bot reply contains a suggestion button with callback `expiry:suggest:14`.

## Smoke Test

- [ ] **Manual end-to-end smoke test** *(stop here — do not mark complete until user confirms)*

  **Setup**: Use a real Telegram group where the bot is running.

  1. **Seed history** (skip if group already has purchase history with expiry):
     - Send `/bought milk`, enter `30` when prompted for expiry. Repeat twice more so there are 3 records for "milk" with ~30-day shelf life.

  2. **Test similar-item suggestion**:
     - Send `/bought milk 1L`
     - Bot replies: "📅 Expiry date for milk?" with inline buttons.
     - **Expected**: At least one button shows `30 days` (or close, from prior records).
     - Tap `30 days` button.
     - **Expected**: Bot confirms purchase with expiry `~30 days from today`.

  3. **Test group-average fallback**:
     - Send `/bought brandnewitem`
     - Bot replies with expiry prompt.
     - **Expected**: One fallback button showing the group average (e.g., `30 days`) + Skip button.

  4. **Test price-capture flow**:
     - Add an item to the shopping list, then mark it as ✓ done.
     - Complete the store + price steps.
     - Bot reaches the expiry step.
     - **Expected**: Suggestion buttons appear (same logic as `/bought`).
     - Tap a suggestion; bot confirms with expiry date.

  5. **Test Skip still works**:
     - Send `/bought anything`, tap **Skip** when expiry prompt appears.
     - **Expected**: Bot confirms without expiry, no crash.

  6. **Test no-history group** (optional, requires a fresh group):
     - In a group with zero purchase history, send `/bought item`.
     - **Expected**: Only **Skip** button appears (no suggestion buttons).
