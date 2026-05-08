## Context

The current "mark as bought" flow (`ShopDoneCallbackHandler`) deletes the item immediately when a user taps ✓ and sends a text confirmation. There is no storage of what was purchased, at what price, or where. The `PendingDialogService<T>` pattern already exists (used by `/buy`) for keyed multi-step dialogs, and `ShoppingItemRepository` handles the active list.

## Goals / Non-Goals

**Goals:**
- After tapping ✓, capture optional store name and price via a 2-step dialog before saving to `PurchaseHistory`
- Item is removed from the active list immediately on ✓ tap (UX stays snappy); price capture is the follow-up
- Store completed purchase records (item name, quantity, store, price, date, buyer) in a new `PurchaseHistory` table
- `/search <query>` returns matching purchase records with price delta (`+N%` / `-N%`) vs. the previous purchase of the same item

**Non-Goals:**
- Currency parsing or multi-currency support (price stored as raw `REAL`; user enters any numeric string)
- Price alerts or budget tracking
- Editing historical records after save
- Capture price during the `/buy` add-to-list flow (price is unknown until purchase)

## Decisions

### Decision 1: Delete item immediately on ✓ tap, then ask for price

**Chosen**: On ✓ tap — delete from `ShoppingItems`, update the list message, then start the price-capture dialog.

**Alternatives considered**:
- Wait to delete until price dialog completes: item lingers on the list during the dialog; confusing for other group members.
- Soft-delete (mark as pending): adds schema complexity with no UX gain.

**Why chosen**: The list stays accurate immediately. Price capture is asynchronous and entirely optional — if the user abandons the dialog, the item is still gone from the list and no history record is written (not an error state).

### Decision 2: Reuse `PendingDialogService<T>` with a new `PriceCaptureDialogState`

**Chosen**: Create `PriceCaptureDialogState` (Step, ItemName, Quantity, BoughtByName, StoreName?) keyed by `(chatId, userId)`. `PriceCaptureStepHandler : IDialogMessageHandler` handles text replies. Two skip callbacks (`price:skip_store`, `price:skip_price`) handled by a new `PriceSkipCallbackHandler`.

**Alternatives considered**:
- Single combined skip callback handler for both skips: one handler with two prefixes requires the dispatcher to support multiple prefixes per handler (it doesn't currently) or a prefix-contains check.
- Inline-only price entry (no text replies): limits input format; `InlineKeyboardButton.WithSwitchInlineQueryCurrentChat` is not appropriate here.

**Why chosen**: Consistent with the existing `/buy` dialog pattern. Zero changes to `UpdateDispatcher` routing logic; `IDialogMessageHandler.CanHandle` returns true when `PriceCaptureDialogState` exists for the user.

### Decision 3: Separate `PurchaseHistory` table, not a column on `ShoppingItems`

**Chosen**: New `PurchaseHistory` table with `(Id, GroupId, ItemName, Quantity, StoreName, Price, PurchasedAt, BoughtByName)`.

**Alternatives considered**:
- Add `Price`, `StoreName`, `PurchasedAt` columns to `ShoppingItems`: items are deleted after purchase; the table is ephemeral. Keeping history there couples two very different lifecycles.
- Separate `Stores` lookup table: over-engineering; store names are free-text.

**Why chosen**: Clean separation — `ShoppingItems` is the active list; `PurchaseHistory` is the ledger. `PurchasedAt` stored as ISO 8601 TEXT (SQLite convention, sortable as string).

### Decision 4: `/search` matches partial name, groups by normalized name, orders by date desc

**Chosen**: `SELECT * FROM PurchaseHistory WHERE GroupId = ? AND lower(ItemName) LIKE lower('%query%') ORDER BY PurchasedAt DESC`. Results grouped client-side by `ItemName` (case-insensitive). Delta computed from the two most-recent records per group that both have a non-null `Price`.

**Alternatives considered**:
- FTS5 (full-text search extension): SQLite FTS5 requires the extension to be compiled in; not guaranteed in the Docker runtime image. LIKE is sufficient for this use case.
- Server-side grouping with GROUP BY: loses the ability to access both the latest and second-latest price per item without a self-join. Client-side grouping on a small result set is fine.

**Why chosen**: Simple, AOT-safe, no extra dependencies.

### Decision 5: Price delta formula

`delta = round((latestPrice - previousPrice) / previousPrice * 100, 1)`

Formatted as `+5.3%` or `-2.1%`. Shown only when both the latest and the previous purchase have a non-null `Price`. No delta shown for the first purchase of an item.

## Sequence Diagram — Mark as Bought with Price Capture

```
User taps [✓ Milk 2л]
  → ShopDoneCallbackHandler
      reads ShoppingItem (name, qty)
      deletes ShoppingItem
      updates list message
      sets PriceCaptureDialogState(step=1, ...)
      AnswerCallbackQuery
      SendMessage: "📍 Where did you buy Milk? (store name)"  + [Skip] button

User types "Magnit"  OR  taps [Skip]
  → PriceCaptureStepHandler (step=1)  OR  PriceSkipCallbackHandler (price:skip_store)
      stores StoreName (or null)
      advances to step=2
      SendMessage: "💰 Price for Milk?" + [Skip] button

User types "89.90"  OR  taps [Skip]
  → PriceCaptureStepHandler (step=2)  OR  PriceSkipCallbackHandler (price:skip_price)
      parses price (or null)
      saves PurchaseHistory record
      clears dialog state
      SendMessage: "✓ Milk recorded" (+ store / price if provided)
```

## Risks / Trade-offs

- [User abandons price dialog] → No `PurchaseHistory` record written; item already removed from list. Acceptable — the dialog is optional. Future: a timeout could clear stale dialog states, but the existing `/buy` pattern has the same gap.
- [Price parsing fails for non-numeric input] → `PriceCaptureStepHandler` replies "That doesn't look like a price — try again or tap [Skip]" and stays on step 2. No retry limit.
- [LIKE search is case-sensitive in SQLite for non-ASCII characters] → `lower()` applied to both sides covers ASCII; Cyrillic characters may still not fold. Acceptable for MVP.
- [PurchasedAt stored as TEXT] → Consistent with SQLite best practices; ISO 8601 strings sort correctly. No risk.

## Migration Plan

1. `DatabaseInitializer.StartAsync` executes `CREATE TABLE IF NOT EXISTS PurchaseHistory (...)`.
2. New `PurchaseHistoryRepository` registered in DI.
3. `ShopDoneCallbackHandler` updated: reads item before delete, starts price-capture dialog.
4. New `PriceCaptureStepHandler`, `PriceSkipCallbackHandler`, `SearchCommandHandler` registered in DI.

**Rollback**: Remove new handlers from DI, drop `PurchaseHistory` table. `ShopDoneCallbackHandler` reverted to instant-delete. No existing data affected.

## Open Questions

- Should price capture be skippable at the entire-dialog level (e.g., a [No thanks] button on the first prompt that bypasses both steps)?
- Should `/search` show all records for a match or just the latest N (e.g., last 5 purchases)?
