## Why

The current `/buy` parser uses `LastIndexOf(' ')` to split the last word as the quantity. This misclassifies unit words:

```
/buy купить отривин 1 штуку
→ item = "купить отривин 1", quantity = "штуку"   ← wrong
```

There is no easy way to fix a bad parse: the user must remove the item and retype the command. Editing existing list items has the same problem — only remove and re-add is possible.

The fix is two-part:
1. **Smart parser** — regex that recognizes trailing quantity patterns (`2 кг`, `1 шт`, `3 пачки`, bare numbers) and strips common prefixes like "купить". Unknown trailing words stay in the name rather than being misidentified as quantities.
2. **Review step before saving** — after every inline `/buy` or dialog step 2, the bot shows a confirmation with [✓ Add] [✏️ Edit] [✕ Cancel]. This gives the user one chance to catch a parse error before the item lands in the list.
3. **Edit existing items** — the `/list` view adds an [✏️] button per item that opens a one-line edit dialog followed by the same confirmation.

## What Changes

- **`BuyInputParser`** (new static class): Smart regex-based parsing of "item name qty" text.
- **`PendingAddService`** (new singleton): Holds `PendingAddItem` records keyed by a short random token until the user confirms or cancels.
- **`PendingEditService`** (new singleton): Same pattern for in-progress item edits.
- **`BuyCommandHandler`**: Uses `BuyInputParser` instead of `LastIndexOf`; routes to review step instead of `AddAsync`.
- **`BuyStepHandler`**: Step 2 completion routes to review instead of directly saving; in one-line edit mode, step 1 parses combined input.
- **`BuyDialogState`**: Gains `IsOneLineMode` flag for the edit-reentry flow.
- **`ShoppingListService`**: Adds [✏️] button per item row (beside existing ✓ and Remove).
- **`ShoppingItemRepository`**: New `UpdateAsync(int itemId, string name, string? quantity)` method.
- **New callback handlers**: `BuyConfirmCallbackHandler`, `BuyEditCallbackHandler`, `BuyCancelCallbackHandler`, `ItemEditCallbackHandler`, `ItemSaveCallbackHandler`, `ItemCancelEditCallbackHandler`.
- **New dialog handler**: `ItemEditStepHandler`.
- **New models**: `EditItemDialogState`, `PendingAddItem`, `PendingEditItem`.
- **New localization keys**: review prompt, button labels, edit prompts, saved/cancelled confirmations.

## Capabilities

### Modified Capabilities

- `buy-item-to-shopping-list`: Add smart parsing and a review/confirmation step before any item is persisted.
- `view-shopping-list`: Add per-item [✏️ Edit] button that opens an edit dialog.

## Impact

- **Code**: ~10 new files; 5 existing files modified; no new external packages.
- **APIs**: No change — same Telegram Bot API calls, no OpenRouter.
- **Dependencies**: No new NuGet packages.
- **Systems**: No schema changes (no new DB columns or tables).
- **Compatibility**: Fully AOT-safe — no reflection, no dynamic dispatch; all new models registered in `BotActionPayloadContext` if serialized.

## Rollback Plan

All new handlers and services are isolated additions. Rollback:
1. Remove new handler/service/model files.
2. Revert `BuyCommandHandler`, `BuyStepHandler`, `BuyDialogState`, `ShoppingListService`, `ShoppingItemRepository` to their previous versions.
3. Remove the new DI registrations from `Program.cs`.
4. Remove new localization keys from `Strings.*.json`.

`UpdateDispatcher`, database, and all other handlers are untouched.

## Affected Teams

Single-user/household bot. No shared infrastructure impact.

## Cross-Cutting Notes

- `PendingAddService` and `PendingEditService` follow the same in-memory keyed session pattern as `PendingDialogService<T>` — registered as `AddSingleton`.
- Callback tokens are 8 hex characters (4 random bytes); callback data stays well within the 64-byte Telegram limit (e.g., `buy:confirm:a1b2c3d4` = 20 bytes).
- All user-facing strings route through `ILocalizer`; no hardcoded literals in handlers.
- `BuySkipCallbackHandler` (explicit "no quantity" choice) is unchanged — no review needed for a deliberate skip.
- `IHistoryRepository.RecordAsync` is called (try/catch wrapped) in confirm and save handlers, not in the review prompt step.
- Group-only guard remains in `BuyCommandHandler` via `GroupRepository.GetOrCreateAsync`.
