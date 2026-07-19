## Context

`ShoppingListService.BuildListAsync` (see `ProductTrackerBot/Services/ShoppingListService.cs:111-249`) currently builds three keyboard blocks in sequence: (1) one row per paged item (`pageSize = 10`), (2) a single pagination row (`[← Previous]`/`[Next →]`), (3) a single filter row holding up to 5 category buttons plus an optional "Все" button (up to 6 in one row — this is what overflows on screen). The message text (`FormatItemsAsText(allItems)`) already lists every item regardless of the button page, so the button page's job is purely "which couple of items can I act on right now," not "show me everything."

## Goals / Non-Goals

**Goals:**
- Give the action-button page a carousel size that's easy to page through so shoppers can act on items without excessive taps.
- Keep the full text listing exactly as-is (no regression to what information is visible).
- Wrap the filter row(s) to at most 2 buttons per row so labels don't truncate/overlap.
- Add clear, non-interactive visual separation between the item-action block, the pagination controls, and the filter block.

**Non-Goals:**
- No change to what "Previous"/"Next" do conceptually (still page through the active/filtered item set) — only the page size shrinks.
- No change to the category-filter *selection* semantics (still index-based callback data, still OR/one-active-set toggle logic) — that's owned by the `product-groups` capability, not this change. This change does, however, now own how the filter button *list itself* is paged (see Decision 5) — the original "no filter pagination" call is superseded because the existing 5-tag display cap silently drops tags 6+ with no way to reach them.
- No swipe/native-carousel Telegram widget — Telegram Bot API has no such control; "carousel" here means small-page pagination via the existing Previous/Next buttons.

## Decisions

### 1. Carousel page size is a new constant, kept separate from any future configurability
`ActionPageSize` is a `private const int` in `ShoppingListService`, distinct from the (removed) `pageSize = 10` local that used to live in `BuildListAsync`. Shipped initially as `2` (a "couple" of items, per the original request); revised to `10` in a same-session follow-up request (see Post-Implementation Adjustments) after live use showed 2 was too small in practice. Alternative considered: make it a `GroupPreferences`-level setting. Still rejected as premature — no request for per-group configurability has been made; a hardcoded constant remains the simplest fix.

### 2. Pagination row carries an inline page indicator; no separate spacer rows
Original design: a non-interactive full-width "spacer" button (label sourced from `ILocalizer` as `list.spacer`, e.g. "▫️▫️▫️") inserted between the item-action block, the pagination row, and the filter block, tapping which did nothing. **Superseded** by a same-session follow-up request: instead of blank spacer rows, the pagination row itself now renders `[← Previous] [n/N] [Next →]` (omitting Previous/Next at the respective page-range ends), with the middle `[n/N]` button using the `noop` callback. This is applied to both the item-pagination row and the tag-pagination row. The visual separation goal from Goal 4 is achieved by the page indicator itself rather than a dedicated blank row, and no keyboard row is spent purely on cosmetics.

Telegram's Bot API still has no concept of a non-button visual divider inside an inline keyboard — every keyboard element must be a button — so the `noop` callback prefix and `NoOpCallbackHandler` (added for the original spacer) are retained and repurposed for the page-indicator button: it answers the callback query (clearing the client-side loading spinner) and takes no further action. The `list.spacer` localization key and the `BuildSpacerRow` helper are removed as dead code now that nothing constructs a standalone spacer row.

Alternative considered (still rejected, as in the original design): reuse the existing `action.cancel` callback for the inert button. Rejected for the same reason — `action:cancel` has real behavior elsewhere; a dedicated `noop` prefix keeps the inert button's contract explicit.

### 3. (Superseded) Spacers were inserted only between blocks that both exist
This decision applied to the original spacer-row design and no longer applies — there are no spacer rows to insert. See Decision 2.

### 4. Filter row wraps at 2 per row via simple chunking
The filter-button list is split into chunks of 2 via `.Chunk(2)` before being appended as separate keyboard rows, rather than one long row.

### 5. Filter/tag list becomes paginated instead of hard-capped at 5
`BuildFilterButtons` today hard-caps at `Math.Min(allTags.Count, 5)` — a group with more than 5 distinct tags simply cannot filter by tag 6+; there is no UI path to reach them. This change removes that cap and replaces it with pagination over the tag list itself, reusing the wrapped 2-per-row layout from Decision 4:

- **`TagPageSize = 6`** (3 rows of 2) — a new `private const int` in `ShoppingListService`, independent of `ActionPageSize`.
- **State carried**: `BuildListAsync` gains an optional `tagPageNumber` parameter (default 1, 1-based, clamped like item pages). It is threaded through to `BuildFilterButtons`, which slices `allTags` to the requested tag page instead of the first 5.
- **Tag pagination row**: when `allTags.Count > TagPageSize`, a `[← Previous]`/`[Next →]`-style row is appended immediately after the wrapped tag-button rows, using a new callback prefix `list_tagpage:{groupChatId}:{activeIndexCsv}:{itemPageNumber}:{newTagPageNumber}` and a new `ListTagPageCallbackHandler`. This mirrors the existing item-pagination row but paginates the *tag list*, not the items.
- **Tag toggle preserves tag page**: `list_filter`'s callback data gains a 4th field — `list_filter:{groupChatId}:{newCsv}:{itemPageNumber}:{tagPageNumber}` — so tapping a tag on tag-page 2 doesn't silently snap back to tag-page 1. `ListFilterCallbackHandler` re-renders on the same tag page it was called from.
- **Item pagination resets tag page to 1**: `list_next`/`list_prev` (unfiltered item paging) do not carry tag-page state — paging through items always re-renders the filter block at tag page 1. This is a deliberate simplification (see Trade-offs) rather than growing every callback's parameter count; tag-page position is treated as ephemeral UI state tied to interacting with the filter block itself, not to item navigation.
- **"All Items" (clear filter) and Cancel stay fixed, outside tag paging**: per explicit decision, both render as always-visible rows below the tag-page block regardless of which tag page is showing — a user should never have to page through tags to reach Cancel or to clear an active filter. "All Items" only renders when `isFiltered` (unchanged from today's "Все" behavior).
- **Callback length**: `list_tagpage`/`list_filter` callback data is bounded by `groupChatId` (≤ ~20 chars incl. sign) + csv of *active* tag indices (small in practice — a handful of small integers) + two small page numbers; comfortably under Telegram's 64-byte `callback_data` limit for realistic tag counts. No new truncation logic is needed beyond the existing `TruncateCategoryLabel` on button text.

Alternative considered: keep the 5-tag cap and treat "more than 5 tags" as out of scope. Rejected — it's a real, currently-silent functionality gap (tags become permanently unreachable), and the tag-pagination UI reuses the exact row-wrapping machinery this change already introduces in Decision 4, so the marginal implementation cost is small.

## Risks / Trade-offs

- **[Risk]** A small carousel page size means more taps to reach an item further down the list → **Mitigation**: the full text listing (unaffected by this change) lets a user see everything at a glance; Previous/Next are one tap each. `ActionPageSize` was revised from 2 to 10 in a same-session follow-up after this trade-off proved too aggressive in practice.
- **[Trade-off]** Item Previous/Next resets tag-page position to 1 rather than preserving it → **Mitigation**: tag-page position is only relevant while actively browsing/toggling tags; groups with ≤6 tags (the common case) never see a tag-pagination row at all, so this only affects the minority with >6 distinct tags.

## Migration Plan

1. Add `NoOpCallbackHandler`, register in `Program.cs` and `TelegramIntegrationTestBase`.
2. Change the action page size constant and chunk the filter row; render the pagination row with an inline page indicator (`[← Previous] [n/N] [Next →]`) for both item and tag pagination.
3. No data/schema changes — deploy is a plain code release.
4. **Rollback**: revert the code deploy; no persisted state is affected.

## Open Questions

- Is 10 the right carousel size, or should it be configurable later? Shipped originally at 2, revised to 10 per a same-session follow-up request; revisit again if usage feedback suggests otherwise.
