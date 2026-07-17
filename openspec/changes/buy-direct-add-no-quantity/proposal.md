## Why

Today, every inline single-item `/buy Name` (or `/buy Name Qty`) shows a "Добавить: X?" review message with Confirm/✏️ Изменить/Cancel buttons before the item is actually saved — even when `BuyInputParser` found no quantity at all (e.g. `/buy Хлеб`), where there's nothing ambiguous to confirm or edit. This adds an extra tap for the common case of adding a plain item with no quantity, and is inconsistent with bulk comma-separated `/buy` (`/buy Хлеб, Молоко`), which already adds items immediately with no review step at all. This change makes single-item `/buy` behave the same way as bulk add when the parser finds no quantity: add immediately, no review.

## What Changes

- In `BuyCommandHandler.HandleAsync`'s inline (non-comma) branch, when `BuyInputParser.Parse` returns `Quantity = null`, skip the `PendingAddService`/review-message step entirely: persist the item immediately via `ShoppingItemRepository.AddAsync`, send the plain "Иван добавил(а) X" confirmation (no buttons), record `ItemAdded` history, and start the category-capture follow-up — mirroring exactly what `BuyConfirmCallbackHandler` already does after a review confirms, and matching bulk add's no-review behavior.
- When `BuyInputParser.Parse` returns a non-null `Quantity` (a number was found), behavior is **unchanged** — the review message with Confirm/✏️ Изменить/Cancel still appears, since there's a parsed quantity worth letting the user double-check or correct before saving.
- **BREAKING**: none — this only removes a confirmation step for a subset of inputs; the item is still saved with the same name and (absence of) quantity as before. No stored data changes shape.

## Capabilities

### Modified Capabilities
- `shopping-list`: the "Add item to shopping list via /buy command" requirement changes — inline single-item `/buy` with no detected quantity now saves immediately instead of showing a review step.

## Impact

- **Handlers**: `BuyCommandHandler.HandleAsync` — add a branch for `quantity is null` that persists directly (duplicating/reusing the persist-and-confirm logic currently in `BuyConfirmCallbackHandler`, ideally factored into a small shared private method or service call to avoid copy-paste between the two handlers). `BuyConfirmCallbackHandler`/`BuyEditCallbackHandler`/`BuyCancelCallbackHandler` and the review flow remain otherwise unchanged for the "quantity found" case.
- **Localization**: no new keys — reuses the existing `buy.item-added` key (already used for the no-quantity confirmation case in `BuyConfirmCallbackHandler`).
- **Rollback plan**: revert the handler change; no schema or persisted-data changes.
- **AOT compatibility**: no reflection or new serialization; a straightforward branch and a persist call already used elsewhere.
- **Affected teams**: none beyond the bot maintainer(s).
- **Cross-cutting decisions relied on**: `IHistoryRepository.RecordAsync` call wrapped in try/catch (unchanged pattern); category-capture follow-up triggered exactly as the other three trigger points already do; all user-facing text via `ILocalizer`.
