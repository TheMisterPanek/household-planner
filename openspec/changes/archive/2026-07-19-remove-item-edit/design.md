## Context

The one-line edit flow (`ItemEditCallbackHandler` → `ItemEditStepHandler` → `ItemSaveCallbackHandler`/`ItemCancelEditCallbackHandler`) lets a user tap ✏️ on a list item, reply with new "name quantity" text, and save or cancel. It was added without a corresponding update to the canonical `shopping-list`/`list-pagination` specs, which still document the item row as just `[✓ Name qty]` / `[✗ Убрать]`. Per direct user feedback, this flow sees no real usage — deleting and re-adding via `/buy` is simpler and is what people actually do.

## Goals / Non-Goals

**Goals:**
- Remove the edit button, its dialog flow, and all now-dead code cleanly (handlers, state types, DI registrations, tests, localization keys).
- Leave the delete (`✗ Убрать`) and mark-bought (`✓`) buttons and their behavior completely unchanged.

**Non-Goals:**
- No replacement editing mechanism (e.g. a re-add shortcut) — deleting and re-adding via `/buy` is already sufficient per the user's own framing.
- No change to `BuyCommandHandler`'s inline review-time "✏️ Изменить" button (`BuyEditCallbackHandler`, callback `buy:edit:{token}`) — that is a distinct feature (editing a not-yet-persisted item during the add-review step, before it's ever saved), not the post-add list-item edit this change removes.

## Decisions

### 1. Full removal, not a feature flag or hidden entry point
Alternative considered: keep the handlers registered but stop rendering the button (dead code path, reachable only by a hand-crafted callback). Rejected — the project's conventions favor deleting genuinely unused code outright rather than leaving unreachable paths around "just in case"; there is no ambiguity here since the user explicitly confirmed this flow is never used.

### 2. Verify no other handler depends on `EditItemDialogState`/`PendingEditService` before deleting
Since `ItemSaveCallbackHandler` also calls `CategoryCaptureService` (the `product-groups` tag-prompt trigger), confirm during implementation that `CategoryCaptureService` itself has no other callers that would break — it's expected to still be called from the other three `/buy`-side trigger points (`BuyConfirmCallbackHandler`, `BuySkipCallbackHandler`, `BuyCommandHandler.HandleBulkAddAsync`), so only the `ItemSaveCallbackHandler`-specific call site goes away, not the service itself.

## Risks / Trade-offs

- **[Risk]** A user who genuinely wants to fix a typo in an item's name loses the in-place fix and must delete + re-add → **Mitigation**: explicitly accepted by the user as the simpler, preferred path; re-adding also re-triggers the tag/category prompt, which arguably improves re-categorization versus the old edit flow's behavior of preserving the existing category untouched unless explicitly changed.
- **[Trade-off]** `multi-item-tags` (not yet implemented) planned to reuse the edit flow as a fourth tag-capture trigger point → **Mitigation**: documented in this proposal's dependency note; `multi-item-tags` should drop that trigger point if this change lands first.

## Migration Plan

1. Confirm (via a repo-wide search) all call sites and references to `ItemEditCallbackHandler`, `ItemEditStepHandler`, `ItemSaveCallbackHandler`, `ItemCancelEditCallbackHandler`, `EditItemDialogState`, `PendingEditService`.
2. Delete the handler files, state/service types, their DI registrations, and their test files.
3. Remove the `item:edit:{item.Id}` button from `ShoppingListService.BuildListAsync`.
4. Remove now-unused localization keys after confirming no remaining references.
5. **Rollback**: revert the deletion commit; no data/schema impact.

## Open Questions

- None — this is a straightforward, user-confirmed removal.
