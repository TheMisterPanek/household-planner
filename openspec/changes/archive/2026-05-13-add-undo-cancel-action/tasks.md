## 1. Database Migration

- [x] 1.1 Add `revert_payload TEXT` column migration to `DatabaseInitializer.StartAsync` (PRAGMA check + ALTER TABLE, ignore duplicate-column exception)
- [x] 1.2 Add `reverted_at TEXT` column migration to `DatabaseInitializer.StartAsync` (same pattern)

## 2. History Repository

- [x] 2.1 Add `revert_payload` and `reverted_at` to the `BotActionHistoryEntry` model
- [x] 2.2 Update `RecordAsync` signature to accept nullable `revertPayloadJson` parameter and write it to the new column
- [x] 2.3 Implement `GetLatestReversibleAsync(chatId, userId, ct)` — queries for most recent row where `revert_payload IS NOT NULL AND reverted_at IS NULL`
- [x] 2.4 Implement `MarkRevertedAsync(entryId, revertedAt, ct)` — sets `reverted_at` on the specified row
- [x] 2.5 Update `IHistoryRepository` interface with the two new methods and updated `RecordAsync` signature
- [x] 2.6 Write unit tests for `GetLatestReversibleAsync`: returns latest reversible entry; returns null when none exist; excludes already-reverted entries
- [x] 2.7 Write unit tests for `MarkRevertedAsync`: sets reverted_at; subsequent `GetLatestReversibleAsync` returns null

## 3. Revert Payload Models

- [x] 3.1 Define `RevertOperation` sealed class hierarchy: `ItemBoughtRevert` (itemId, listId), `ItemRemovedRevert` (itemId, name, listId), `ListArchivedRevert` (listId)
- [x] 3.2 Register all `RevertOperation` subclasses with `[JsonSerializable]` in the source-generated `BotJsonContext`
- [x] 3.3 Add `BotActionType` values for any missing action types: `ListArchived` (if not already present)

## 4. UndoService

- [x] 4.1 Create `IUndoService` interface with `UndoLastAsync(chatId, userId, ct)` returning a localization key string (success/nothing/error)
- [x] 4.2 Implement `UndoService`: fetch latest reversible entry → deserialize `revert_payload` → dispatch to repository inverse → call `MarkRevertedAsync`
- [x] 4.3 Handle `ItemBoughtRevert`: restore item to not-bought state via shopping list repository
- [x] 4.4 Handle `ItemRemovedRevert`: re-insert item with original name and list association
- [x] 4.5 Handle `ListArchivedRevert`: unarchive list via shopping list repository
- [x] 4.6 Wrap deserialization in try/catch: log Warning and return "undo.error" key on failure
- [x] 4.7 Register `UndoService` in `Program.cs` as `AddScoped<IUndoService, UndoService>()`
- [x] 4.8 Write unit tests for `UndoService`: success for each supported action type; "undo.nothing" when no entry; "undo.error" on deserialization failure; "undo.error" for unsupported action type

## 5. /undo Command Handler

- [x] 5.1 Create `UndoCommandHandler` implementing `ICommandHandler` with `Command = "/undo"`
- [x] 5.2 Call `IUndoService.UndoLastAsync` and reply with the returned localization key via `ILocalizer.Get(chatId, key)`
- [x] 5.3 Register `UndoCommandHandler` in `Program.cs` as `AddScoped<ICommandHandler, UndoCommandHandler>()`
- [x] 5.4 Register `/undo` with `BotCommandsRegistrar` (add to command list sent to Telegram)
- [x] 5.5 Write unit tests for `UndoCommandHandler`: delegates to `UndoService`; sends localized reply for each outcome

## 6. Action Cancel Button

- [x] 6.1 Create `ActionCancelCallbackHandler` implementing `ICallbackHandler` with `CallbackPrefix = "action:cancel"`
- [x] 6.2 In handler: call `DeleteMessageAsync`; catch `ApiRequestException` and fall back to `EditMessageReplyMarkupAsync(null)`
- [x] 6.3 Register `ActionCancelCallbackHandler` in `Program.cs` as `AddScoped<ICallbackHandler, ActionCancelCallbackHandler>()`
- [x] 6.4 Add Cancel button (callback `action:cancel`, label from `ILocalizer.Get(chatId, "action.cancel")`) as the last keyboard row in: mark-item-done message, archive-list message, remove-item message, meal-merge message
- [x] 6.5 Write unit tests for `ActionCancelCallbackHandler`: deletes message on success; falls back to keyboard removal when delete throws; does not modify any data

## 7. Update Existing Handlers to Pass Revert Payloads

- [x] 7.1 In `ItemBought` callback handler: serialize `ItemBoughtRevert` and pass as `revertPayloadJson` to `RecordAsync`
- [x] 7.2 In `ItemRemoved` callback handler: serialize `ItemRemovedRevert` (capture item name before deletion) and pass as `revertPayloadJson` to `RecordAsync`
- [x] 7.3 In `ListArchived` callback handler: serialize `ListArchivedRevert` and pass as `revertPayloadJson` to `RecordAsync`
- [x] 7.4 For non-reversible actions (e.g., `ListViewed`, `HistoryViewed`): pass `null` for `revertPayloadJson`
- [x] 7.5 Write unit tests verifying each updated handler passes the correct `revertPayloadJson` to `RecordAsync`

## 8. Localization

- [x] 8.1 Add `"action.cancel"` key to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`
- [x] 8.2 Add `"undo.success"` key to all three locale files
- [x] 8.3 Add `"undo.nothing"` key to all three locale files
- [x] 8.4 Add `"undo.error"` key to all three locale files
