## Context

`BuyCommandHandler.HandleAsync` (`ProductTrackerBot/Handlers/BuyCommandHandler.cs:74-135`) handles three inline shapes today: comma-separated bulk (adds immediately, no review, via `HandleBulkAddAsync`), and single-item (with or without a parsed quantity), which always shows a "Добавить: X?" review with Confirm/Edit/Cancel via `PendingAddService` + `BuyConfirmCallbackHandler`/`BuyEditCallbackHandler`/`BuyCancelCallbackHandler`. `BuyInputParser.Parse` already distinguishes the two single-item cases via `Quantity is null`. The review step's value is in letting a user catch and fix a misparsed quantity (e.g. "2л" split wrong) before it's saved — when there's no quantity at all, there's nothing for Edit to meaningfully fix, and Confirm/Cancel become pure friction matching what bulk add already skips.

## Goals / Non-Goals

**Goals:**
- Single-item `/buy` with no detected quantity saves immediately, with the same confirmation message, history recording, and category-capture trigger as the existing review-confirm path.
- Single-item `/buy` with a detected quantity keeps today's review step unchanged.

**Non-Goals:**
- No change to `BuyInputParser`'s parsing logic itself (that's the separate, already-existing `smart-buy-count-parser` change) — this only changes what `BuyCommandHandler` does with the parser's `Quantity is null` result.
- No change to the bulk (comma-separated) path, the two-step dialog path (`/buy` with no arguments), or the skip-quantity dialog path — all already persist without a review step.

## Decisions

### 1. Extract the persist-and-confirm logic into a shared private method, called from both the new direct-add branch and `BuyConfirmCallbackHandler`
Alternative considered: duplicate the ~15 lines (AddAsync, confirmation text, history record, category-capture start) directly in `BuyCommandHandler.HandleAsync`. Rejected — the two call sites (direct-add-on-no-quantity in `BuyCommandHandler`, and confirm-after-review in `BuyConfirmCallbackHandler`) need identical behavior; a shared private helper (or a small method added to `ShoppingItemRepository`/a service both handlers already depend on) avoids drift between them. Since `BuyConfirmCallbackHandler` is a separate class, the shared logic is placed on `CategoryCaptureService` or a similarly-scoped existing service both handlers already inject, rather than introducing a new one purely for this.

### 2. Branch strictly on `Quantity is null`, not on any other heuristic
Alternative considered: also skip review for single-word items shorter than N characters, or other heuristics. Rejected — `BuyInputParser.Parse`'s `Quantity is null` result is already the exact signal for "nothing quantity-shaped was found," which is precisely the condition described in the request; no additional heuristic is needed or justified.

## Risks / Trade-offs

- **[Risk]** A user who wanted to double-check the item name before saving (not just the quantity) loses that chance for no-quantity adds → **Mitigation**: this exactly matches bulk add's existing, already-accepted behavior for the same reason; deleting and re-adding remains available if a mistake is made (per `remove-item-edit`'s framing).
- **[Trade-off]** Two call sites (direct-add branch, review-confirm handler) must stay behaviorally identical → **Mitigation**: addressed by Decision 1's shared-method extraction.

## Migration Plan

1. Extract the persist-and-confirm logic from `BuyConfirmCallbackHandler` into a shared method callable from `BuyCommandHandler`.
2. Add the `quantity is null` direct-add branch in `BuyCommandHandler.HandleAsync`, calling the shared method instead of building a `PendingAddService` token/review message.
3. No data/schema changes — deploy is a plain code release.
4. **Rollback**: revert the code deploy; no persisted state is affected.

## Open Questions

- None.
