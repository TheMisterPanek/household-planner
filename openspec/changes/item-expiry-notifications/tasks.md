## 1. Database Migration

- [ ] 1.1 Add `exp_date` column detection in `DatabaseInitializer`: query `PRAGMA table_info(ShoppingItems)` and run `ALTER TABLE ShoppingItems ADD COLUMN exp_date TEXT` if column is absent
- [ ] 1.2 Handle duplicate-column `SqliteException` gracefully (catch and ignore) to make startup idempotent under concurrent restarts

## 2. Model & Repository

- [ ] 2.1 Add `ExpDate` (`DateOnly?`) property to `ShoppingItem` record
- [ ] 2.2 Update `ShoppingItemRepository.AddAsync` signature to accept `DateOnly? expDate` and include `exp_date` in the `INSERT`
- [ ] 2.3 Update `ShoppingItemRepository.GetAllAsync` to read `exp_date` column and map to `ExpDate`
- [ ] 2.4 Add `ShoppingItemRepository.GetItemsWithExpiryAsync(int groupId)` returning items where `exp_date IS NOT NULL`
- [ ] 2.5 Write unit tests for `AddAsync` with and without expiry date
- [ ] 2.6 Write unit tests for `GetAllAsync` mapping of `exp_date` (null and non-null)
- [ ] 2.7 Write unit tests for `GetItemsWithExpiryAsync` filtering

## 3. /buy Dialog Extension

- [ ] 3.1 Add `Step = 3` state and `ExpDate` (`DateOnly?`) field to `BuyDialogState`
- [ ] 3.2 Refactor `BuyStepHandler.HandleStep2Async`: advance `state.Step = 3` and prompt "Срок годности? (например, 15.05.2026)" with `[Пропустить]` button — do NOT call `AddAsync` yet
- [ ] 3.3 Add `BuyStepHandler.HandleStep3Async`: parse `dd.MM.yyyy` input, on success call `FinishDialogAsync`; on invalid format reply with error and stay at step 3
- [ ] 3.4 Update `BuyStepHandler.FinishDialogAsync` to pass `state.ExpDate` to `itemRepository.AddAsync` and include date in confirmation text when set
- [ ] 3.5 Refactor `BuySkipCallbackHandler`: instead of saving the item, set `state.Quantity = null`, advance `state.Step = 3`, and prompt for expiry date with `[Пропустить]` button
- [ ] 3.6 Create `BuySkipExpiryCallbackHandler` (`buy:skip_expiry`): calls `FinishDialogAsync` with `expDate = null`
- [ ] 3.7 Register `BuySkipExpiryCallbackHandler` in DI
- [ ] 3.8 Write unit tests for `BuyStepHandler` step 3: valid date, invalid date, skip
- [ ] 3.9 Write unit tests for refactored `BuySkipCallbackHandler` (transitions to step 3 instead of saving)
- [ ] 3.10 Write unit tests for `BuySkipExpiryCallbackHandler`

## 4. /list Display Update

- [ ] 4.1 Update `ShoppingListService.BuildListAsync` to append `(до DD.MM)` to item labels when `ExpDate != null`
- [ ] 4.2 Prefix items with `ExpDate <= today` with ⚠️ in the text body
- [ ] 4.3 Write unit tests for `ShoppingListService` with items having expiry dates (normal, expiring today, expired, null)

## 5. Expiry Notification Service

- [ ] 5.1 Create `ExpiryNotificationService` with method `BuildSummaryAsync(long chatId, DateOnly today)` that queries items via `GetItemsWithExpiryAsync`, classifies them into four buckets (expired / today / ≤3 days / 4–7 days), and returns a formatted message string or null if nothing to report
- [ ] 5.2 Implement message formatting: header "📋 Сводка по срокам годности:" followed by sections for each non-empty bucket
- [ ] 5.3 Write unit tests for `ExpiryNotificationService.BuildSummaryAsync`: all four bucket categories, mixed items, no reportable items (returns null)
- [ ] 5.4 Write unit tests for item line formatting (with quantity, without quantity, expired items)

## 6. Scheduled Notification Job

- [ ] 6.1 Create `ExpiryNotificationJob : IHostedService` using `PeriodicTimer` with 24-hour interval; calculate initial delay to first fire time from `NOTIFY_TIME_UTC` env var (default "09:00")
- [ ] 6.2 Implement job loop: enumerate all groups via `GroupRepository`, call `ExpiryNotificationService.BuildSummaryAsync` per group, send message via `ITelegramBotClient` if non-null
- [ ] 6.3 Add per-group exception handling: catch `Exception`, log at `Warning` with `ChatId`, continue loop
- [ ] 6.4 Register `ExpiryNotificationJob` as `IHostedService` in `Program.cs`
- [ ] 6.5 Add `NOTIFY_TIME_UTC` to `.env.example` / docker-compose env documentation
- [ ] 6.6 Write unit tests for `ExpiryNotificationJob`: verifies it calls `BuildSummaryAsync` for each group and sends only when result is non-null
- [ ] 6.7 Write unit tests for error isolation: one group throwing does not prevent others from being processed
