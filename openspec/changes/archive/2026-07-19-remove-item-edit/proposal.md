## Why

The per-item "✏️ Изменить" (edit) button on the `/list` message opens a one-line rename/requantify dialog that, per direct user feedback, is never used in practice — it's simpler to delete an item and re-add it with `/buy` than to edit it in place. It also crowds the item row to 3 buttons, working against the cleaner 2-button-per-row layout wanted for the list. Removing it simplifies the keyboard and the codebase, and happens to restore the item row to the 2-button shape (`[✓ Name qty]` / `[✗ Убрать]`) already documented in the canonical `shopping-list`/`list-pagination` specs — the edit button was added without ever updating those specs, so this change also closes that documentation gap.

## What Changes

- Remove the "✏️ Изменить" button from each item row on the `/list` message.
- Remove the entire one-line edit flow: `ItemEditCallbackHandler` (opens the edit dialog), `ItemEditStepHandler` (captures the new name/quantity text), `ItemSaveCallbackHandler` (confirms and persists), `ItemCancelEditCallbackHandler` (cancels), the `EditItemDialogState`/`PendingEditService` state, and the `item.edit-button`/related edit-flow localization keys.
- **BREAKING**: none for end users beyond the button's removal — no data model changes; existing items are unaffected, only the ability to edit one in place is removed (renaming/requantifying now requires deleting and re-adding via `/buy`).

## Capabilities

### Modified Capabilities
- `shopping-list`: adds an explicit requirement documenting that item rows expose no edit control (closing the gap that let the undocumented edit button exist without ever being specified) — the canonical item-row shape (`[✓ Name qty]` / `[✗ Убрать]`) was already correct and unchanged.

## Impact

- **Handlers**: delete `ItemEditCallbackHandler.cs`, `ItemEditStepHandler.cs`, `ItemSaveCallbackHandler.cs`, `ItemCancelEditCallbackHandler.cs` and their test files; remove their DI registrations from `Program.cs` and their wiring from `TelegramIntegrationTestBase`.
- **Models/Services**: delete `EditItemDialogState` and `PendingEditService` (confirm no other handler depends on them before deleting — see tasks).
- **Services**: `ShoppingListService.BuildListAsync` drops the `item:edit:{item.Id}` button from each item row.
- **Localization**: remove `item.edit-button` and any edit-flow-only keys (e.g. edit prompt/confirmation text) from `Strings.en.json`/`Strings.ru.json`/`Strings.pl.json`, after confirming no remaining references.
- **Rollback plan**: revert the code deletion commit; no schema or persisted-data changes are involved, so rollback is a plain code revert with no data implications.
- **AOT compatibility**: removal-only change; no new reflection or serialization introduced.
- **Affected teams**: none beyond the bot maintainer(s).
- **Cross-cutting decisions relied on**: none new — this is a pure removal.
- **Dependency note — coordinate with `openspec/changes/product-groups/` (implemented, not yet archived) and `openspec/changes/multi-item-tags/` (not yet implemented)**: `product-groups`' `product-tags` spec ("Prompt for a category after an item is added") and `multi-item-tags`' `item-tags` spec (task 4.2, "Tag prompt follows an item edit" scenario) both assume `ItemSaveCallbackHandler` exists as a trigger point for the category/tag-capture prompt. If this change ships first, both of those specs need their edit-triggered scenario dropped — categorization/tagging still works via the other three trigger points (inline single add, bulk add, skip-quantity dialog path); only the "edit re-opens the prompt" path goes away.
