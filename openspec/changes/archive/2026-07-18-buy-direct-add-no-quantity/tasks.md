## 1. Extract Shared Persist-and-Confirm Logic

- [x] 1.1 Extract the persist-and-confirm logic from `BuyConfirmCallbackHandler.HandleAsync` (AddAsync, confirmation text, history record, category-capture start) into a shared method on a service already injected into both `BuyCommandHandler` and `BuyConfirmCallbackHandler` (e.g. `CategoryCaptureService` or a small new `BuyAddService`)
- [x] 1.2 Update `BuyConfirmCallbackHandler` to call the shared method instead of inlining the logic
- [x] 1.3 Unit tests: shared method persists the item, sends the correct confirmation text (with/without quantity), records history, and starts category capture

## 2. Direct-Add Branch in BuyCommandHandler

- [x] 2.1 In `BuyCommandHandler.HandleAsync`'s inline single-item branch, after `BuyInputParser.Parse`, branch on `quantity is null`: call the shared persist-and-confirm method directly instead of creating a `PendingAddService` token/review message
- [x] 2.2 Keep the existing review-message path unchanged for the `quantity is not null` case
- [x] 2.3 Unit tests: `/buy Молоко` (no quantity) persists immediately with no review message sent, correct confirmation text, history recorded, category capture started; `/buy Молоко 2л` (quantity found) still shows the review message and does not persist until Confirm

## 3. Integration Tests

- [x] 3.1 Integration test: `/buy Молоко` — item appears in the shopping list immediately, no Confirm/Edit/Cancel message is sent, category-capture prompt follows
- [x] 3.2 Integration test: `/buy Молоко 2л` — review message with Confirm/✏️ Изменить/Cancel is sent, item not persisted until Confirm is tapped (unchanged behavior, regression check)
- [x] 3.3 Integration test: `/buy Молоко` followed immediately by tapping any stale review-related callback (if a prior review was pending) does not double-add or error

## 4. Documentation

- [x] 4.1 Update the Landing page's `/buy` description if it currently states that every inline add shows a review step (n/a: no Landing page source exists in this repo — it's deployed separately per the archived `vps-landing-page` change; nothing to update here)

## 5. Manual Smoke Test (do not mark complete without user confirmation)

- [x] 5.1 In a real Telegram group chat: send `/buy Хлеб` and confirm it's added immediately with no Confirm/Edit/Cancel buttons, followed by the category prompt; send `/buy Молоко 2л` and confirm the review message with Confirm/✏️ Изменить/Cancel still appears and the item isn't added until Confirm is tapped. **Leave this task open until the user confirms the smoke test passed.**
