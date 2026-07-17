## Context

`ShoppingListService.BuildListAsync` (see `ProductTrackerBot/Services/ShoppingListService.cs:111-249`) currently builds three keyboard blocks in sequence: (1) one row per paged item (`pageSize = 10`), (2) a single pagination row (`[← Previous]`/`[Next →]`), (3) a single filter row holding up to 5 category buttons plus an optional "Все" button (up to 6 in one row — this is what overflows on screen). The message text (`FormatItemsAsText(allItems)`) already lists every item regardless of the button page, so the button page's job is purely "which couple of items can I act on right now," not "show me everything."

## Goals / Non-Goals

**Goals:**
- Shrink the action-button page to a small carousel size so shoppers interact with a couple of items at a time.
- Keep the full text listing exactly as-is (no regression to what information is visible).
- Wrap the filter row(s) to at most 2 buttons per row so labels don't truncate/overlap.
- Add clear, non-interactive visual separation between the three keyboard blocks.

**Non-Goals:**
- No change to what "Previous"/"Next" do conceptually (still page through the active/filtered item set) — only the page size shrinks.
- No change to the category-filter *selection* logic (still one active category, still index-based callback data) — that's owned by the `product-groups`/`multi-item-tags` capability, not this change.
- No swipe/native-carousel Telegram widget — Telegram Bot API has no such control; "carousel" here means small-page pagination via the existing Previous/Next buttons.

## Decisions

### 1. Carousel page size is a new constant, kept separate from any future configurability
Set `pageSize = 2` (a "couple" of items, per the request) as a `private const int ActionPageSize` in `ShoppingListService`, distinct from the existing `pageSize` local in `BuildListAsync`. Alternative considered: make it a `GroupPreferences`-level setting. Rejected as premature — no request for per-group configurability was made; a hardcoded small constant is the simplest fix and matches how `pageSize = 10` was already a hardcoded local before this change.

### 2. Spacer row is a single full-width button with a neutral label, routed to a new no-op handler
Telegram's Bot API has no concept of a non-button visual divider inside an inline keyboard — every keyboard element must be a button. The closest to "does nothing, just visual" is a button whose callback handler immediately answers the callback query (to clear the client-side loading spinner) and takes no further action. A new `NoOpCallbackHandler` (prefix `noop`) covers this; the spacer button's label is a short neutral glyph (e.g. "▫️▫️▫️" or an em-dash) sourced from `ILocalizer` as `list.spacer`, not hardcoded.

Alternative considered: reuse the existing `action.cancel` callback (already routed to a no-op-like cancel behavior) for the spacer. Rejected — `action:cancel` has real behavior (clears/dismisses state elsewhere in the codebase); reusing it for a purely cosmetic spacer risks accidentally triggering cancel semantics if that handler's behavior changes later. A dedicated `noop` prefix keeps the spacer's contract explicit and inert by construction.

### 3. Spacers are inserted only between blocks that both exist
`BuildListAsync` already conditionally omits the pagination row (`totalPages <= 1`) and the filter row (no categories). The spacer insertion follows the same conditionals: a spacer row is appended after the item-action rows only if a pagination row and/or filter row follows; a spacer is appended after the pagination row only if a filter row follows. This avoids a dangling spacer immediately before the final Cancel row when there's nothing substantive to separate.

### 4. Filter row wraps at 2 per row via simple chunking
The existing filter-button list (still capped at 5 categories + optional "Все", per the existing `product-groups` design) is split into chunks of 2 via `.Chunk(2)` before being appended as separate keyboard rows, rather than one long row. This is a pure rendering change — no change to how many categories are computed or which ones are shown.

## Risks / Trade-offs

- **[Risk]** A carousel page size of 2 means more taps to reach an item further down the list → **Mitigation**: the full text listing (unaffected by this change) lets a user see everything at a glance; Previous/Next are one tap each, and this trades fewer visible action rows for a cleaner, non-overflowing layout, per the explicit request.
- **[Trade-off]** Every `/list` render now includes 1-2 extra spacer rows, slightly lengthening the keyboard → **Mitigation**: spacer rows are single buttons (minimal vertical footprint), and only appear between blocks that both exist, so short lists (no pagination, no filters) get zero extra rows.

## Migration Plan

1. Add `NoOpCallbackHandler`, register in `Program.cs` and `TelegramIntegrationTestBase`.
2. Change the action page size constant and chunk the filter row; insert conditional spacer rows.
3. No data/schema changes — deploy is a plain code release.
4. **Rollback**: revert the code deploy; no persisted state is affected.

## Open Questions

- Is 2 the right carousel size, or should it be configurable later (e.g. 3-5)? Ship with 2 per the explicit request; revisit if usage feedback suggests otherwise.
