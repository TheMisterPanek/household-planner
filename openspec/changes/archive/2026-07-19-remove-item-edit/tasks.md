## 1. Audit Before Removal

- [x] 1.1 Search the repo for every reference to `ItemEditCallbackHandler`, `ItemEditStepHandler`, `ItemSaveCallbackHandler`, `ItemCancelEditCallbackHandler`, `EditItemDialogState`, `PendingEditService`, and the `item:edit:` / `item.edit-button` string literals
- [x] 1.2 Confirm `CategoryCaptureService` (called from `ItemSaveCallbackHandler`) has other live callers (`BuyConfirmCallbackHandler`, `BuySkipCallbackHandler`, `BuyCommandHandler.HandleBulkAddAsync`) so the service itself is not deleted, only this call site

## 2. Remove Handlers and State

- [x] 2.1 Delete `ItemEditCallbackHandler.cs`, `ItemEditStepHandler.cs`, `ItemSaveCallbackHandler.cs`, `ItemCancelEditCallbackHandler.cs` and their corresponding test files in `ProductTrackerBot.Tests/Handlers/`
- [x] 2.2 Delete `EditItemDialogState` model and `PendingEditService` (after confirming per task 1.1 that nothing else references them)
- [x] 2.3 Remove their DI registrations from `Program.cs`
- [x] 2.4 Remove their wiring from `TelegramIntegrationTestBase`

## 3. Update the List Keyboard

- [x] 3.1 Remove the `item:edit:{item.Id}` button from `ShoppingListService.BuildListAsync`'s item row construction
- [x] 3.2 Update existing `ShoppingListService`/list-related unit tests that assert on 3-button item rows to expect 2 buttons

## 4. Localization Cleanup

- [x] 4.1 Remove `item.edit-button` and any edit-flow-only prompt/confirmation keys from `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`, after confirming (per task 1.1) no remaining code references

## 5. Integration Tests

- [x] 5.1 Update or remove integration tests that exercised the one-line edit flow (rename/requantify via ✏️)
- [x] 5.2 Integration test: `/list` item rows render with exactly 2 buttons (`[✓ Name qty]`, `[✗ Убрать]`), no edit button
- [x] 5.3 Integration test: a callback with legacy `item:edit:` data is dispatched and produces no dialog, no error, no state change

## 6. Documentation

- [x] 6.1 Update the Landing page to remove any mention of in-place item editing, if present — N/A, no Landing page exists in this repo (removed, Telegram bot only per CLAUDE.md)

## 7. Manual Smoke Test (do not mark complete without user confirmation)

- [x] 7.1 In a real Telegram group chat: send `/list` with a few items and confirm each row shows only `[✓ Name qty]` and `[✗ Убрать]` — no pencil/edit button; confirm marking an item bought and removing an item both still work exactly as before. **Leave this task open until the user confirms the smoke test passed.**
