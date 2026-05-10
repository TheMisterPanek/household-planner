## 1. Schema and Domain Models

- [x] 1.1 In `DatabaseInitializer.StartAsync`, add `PRAGMA table_info(PurchaseHistory)` check and `ALTER TABLE PurchaseHistory ADD COLUMN exp_date TEXT NULL` migration (catch `SqliteException` on duplicate-column and ignore)
- [x] 1.2 Add `ExpDate` (`DateOnly?`) property to `PurchaseRecord` model
- [x] 1.3 Add `ExpDate` (`DateOnly?`) property to `PriceCaptureDialogState` model; document step 3 = awaiting expiry date in the existing step doc-comment

## 2. Repository Updates

- [x] 2.1 Update `PurchaseHistoryRepository.AddAsync` to include `exp_date` in the INSERT statement (write `NULL` when `ExpDate` is null, ISO-8601 `yyyy-MM-dd` string when set)
- [x] 2.2 Add `PurchaseHistoryRepository.GetItemsWithExpiryAsync(int groupId)` that returns a list of `(ItemName, Quantity, ExpDate)` tuples (or a lightweight record) for all rows where `exp_date IS NOT NULL`

## 3. Localization Keys

- [x] 3.1 Add `shop.expiry-prompt` to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json` (e.g. "📅 Expiry date for {item}? Enter days (e.g. 30) or a date")
- [x] 3.2 Add `shop.skip` (or reuse `buy.skip`) as the Skip button label for the expiry step
- [x] 3.3 Add `shop.done-with-expiry` confirmation suffix to all three locale files (e.g. ", expires {expiry}")
- [x] 3.4 Add `shop.invalid-date` error message to all three locale files (mirrors `buy.invalid-date`)

## 4. Price-Capture Step Handler — Step 3

- [x] 4.1 In `PriceCaptureStepHandler.HandleAsync`, add `case 3` that calls a new `HandleStep3Async` method
- [x] 4.2 Implement `HandleStep3Async`: parse the text input using the same date logic as `BuyStepHandler.ParseExpiryInput` (days int, "N days/weeks/months" in EN + RU); on invalid input reply with `shop.invalid-date` and return without advancing
- [x] 4.3 On valid date input, set `state.ExpDate` and call `FinishDialogAsync`
- [x] 4.4 Extract `FinishDialogAsync` to accept `ExpDate?` parameter and pass it into `PurchaseRecord.ExpDate` when saving; update the confirmation message to append expiry suffix when set

## 5. Price Skip Callback Handler — Step 2 → Step 3 and Skip Expiry

- [x] 5.1 In `PriceSkipCallbackHandler.HandleAsync`, change the `step == 2` branch: instead of saving and finishing, advance `state.Step = 3`, update state, answer callback, edit the price message to show "Skipped", then send the expiry prompt with a `[Skip]` button (callback: `price:skip_expiry`)
- [x] 5.2 Add `step == 3` branch (triggered by `price:skip_expiry`): save `PurchaseRecord` with `ExpDate = null`, clear dialog state, answer callback, send confirmation message

## 6. Expiry Notification Service — Bought Items

- [x] 6.1 Inject `PurchaseHistoryRepository` into `ExpiryNotificationService`
- [x] 6.2 In `BuildSummaryAsync`, call `GetItemsWithExpiryAsync(groupId)` and merge results with planned-item results before bucketing into expired / expiring-today / expiring-soon / expiring-this-week sections

## 7. DI Registration

- [x] 7.1 Verify `PurchaseHistoryRepository` is already registered in `Program.cs`; if not, register it as `AddSingleton` (it is stateless)

## 8. Unit Tests — Repository

- [x] 8.1 Unit tests for `PurchaseHistoryRepository.AddAsync`: verify `exp_date` is stored and retrieved correctly when set; verify `NULL` is stored when `ExpDate` is null (use `:memory:` SQLite)
- [x] 8.2 Unit test for `PurchaseHistoryRepository.GetItemsWithExpiryAsync`: returns only rows with non-null `exp_date`; ignores rows without

## 9. Unit Tests — Step Handler

- [x] 9.1 Unit tests for `PriceCaptureStepHandler` step 3: valid day count input advances to finish; invalid text replies with error and stays on step 3; `"7 days"` / `"7 дней"` parses correctly; blank input replies with error
- [x] 9.2 Unit test: when `FinishDialogAsync` receives a non-null `ExpDate`, the saved `PurchaseRecord` has the correct `ExpDate`; confirmation message includes expiry suffix

## 10. Unit Tests — Skip Callback Handler

- [x] 10.1 Unit test: `PriceSkipCallbackHandler` with `price:skip_price` on step 2 → state advances to step 3 and expiry prompt is sent (not finished)
- [x] 10.2 Unit test: `PriceSkipCallbackHandler` with `price:skip_expiry` on step 3 → dialog cleared, `PurchaseRecord` saved with null `ExpDate`, confirmation sent

## 11. Unit Tests — Expiry Notification Service

- [x] 11.1 Unit test: `ExpiryNotificationService.BuildSummaryAsync` includes items from `PurchaseHistory` with upcoming expiry dates in the correct bucket
- [x] 11.2 Unit test: items from both `ShoppingItems` and `PurchaseHistory` appear in the merged summary without duplication
