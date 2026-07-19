## 1. Carousel Page Size

- [x] 1.1 Extract a `private const int ActionPageSize = 2;` in `ShoppingListService`, replacing the hardcoded `const int pageSize = 10;` local in `BuildListAsync`
- [x] 1.2 Verify `FormatItemsAsText(allItems)` continues to render the full, unpaginated item set regardless of `ActionPageSize` (no code change expected — confirm with a test)
- [x] 1.3 Unit tests: `GetPagedItemsAsync`/`BuildListAsync` with `ActionPageSize = 2` — correct item count per page, correct total-pages calculation, footer text reflects the new page size

## 2. No-Op Spacer Button

- [x] 2.1 Add `list.spacer` localization key (short neutral label, e.g. "▫️▫️▫️") to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`
- [x] 2.2 Add `NoOpCallbackHandler` (`ICallbackHandler`, prefix `noop`): answers the callback query, takes no other action
- [x] 2.3 Register `NoOpCallbackHandler` in `Program.cs` and wire it into `TelegramIntegrationTestBase`
- [x] 2.4 Unit tests: `NoOpCallbackHandler` answers the callback and performs no repository/state calls

## 3. Keyboard Layout Changes

- [x] 3.1 In `BuildListAsync`, chunk the category filter buttons into rows of at most 2 (`.Chunk(2)`) instead of a single row
- [x] 3.2 Insert a spacer row (using the `noop` callback and `list.spacer` label) between the item action-button rows and the pagination row, only when a pagination row is rendered
- [x] 3.3 Insert a spacer row between the pagination row (or, if absent, the item action-button rows) and the filter row(s), only when at least one filter row is rendered
- [x] 3.4 Ensure no spacer rows are inserted when neither a pagination row nor a filter row follows (list has ≤ `ActionPageSize` items and no categorized items)
- [x] 3.5 Unit tests: keyboard structure for (a) short list, no pagination, no filters — no spacers; (b) pagination present, no filters — one spacer; (c) no pagination, filters present — one spacer; (d) both present — two spacers; (e) filter row wrapping with 5 tags renders as 3 rows of at most 2

## 4. Integration Tests

- [x] 4.1 Integration test: `/list` with 5 items — first carousel page shows exactly 2 action rows, text body lists all 5 items, `[Next →]` present
- [x] 4.2 Integration test: tapping `[Next →]`/`[← Previous]` pages through the carousel correctly at the new page size
- [x] 4.3 Integration test: `/list` with categorized items and multiple carousel pages — spacer rows appear in the expected positions and filter buttons render wrapped
- [x] 4.4 Integration test: tapping a spacer button — no error, no state change, list message unchanged

## 5. Tag Filter Pagination

- [x] 5.1 Add `private const int TagPageSize = 6;` in `ShoppingListService`; remove the `Math.Min(allTags.Count, 5)` cap in `BuildFilterButtons`
- [x] 5.2 Add an optional `tagPageNumber` parameter (default 1) to `BuildListAsync`, threaded through to `BuildFilterButtons`; clamp out-of-range values to 1 like item pagination does
- [x] 5.3 Slice `allTags` to the requested tag page (`TagPageSize` per page) in `BuildFilterButtons` instead of always showing the first 5
- [x] 5.4 When `allTags.Count > TagPageSize`, append a tag-pagination row (`[← Previous]`/`[Next →]`) after the wrapped tag rows, using callback prefix `list_tagpage:{groupChatId}:{activeIndexCsv}:{itemPageNumber}:{newTagPageNumber}`
- [x] 5.5 Add `ListTagPageCallbackHandler` (`ICallbackHandler`, prefix `list_tagpage`): parses the 4-field callback data, re-renders `BuildListAsync` with the new tag page, current item page, and current active filter preserved
- [x] 5.6 Register `ListTagPageCallbackHandler` in `Program.cs` and wire it into `TelegramIntegrationTestBase`
- [x] 5.7 Update `ListFilterCallbackHandler`'s callback-data format to a 4th field (tag page number) so toggling a tag preserves the current tag page instead of resetting to page 1
- [x] 5.8 Ensure `list_next`/`list_prev` (unfiltered item pagination) continue to reset the tag page to 1 on render (no callback-data change needed there — just confirm `BuildListAsync`'s default `tagPageNumber = 1` applies when they call it)
- [x] 5.9 Render "All Items" (clear-filter) and "Cancel" as fixed rows below the tag-page block, unaffected by which tag page is showing
- [x] 5.10 Add `list.tags-label` localization key ("Tags") to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json` for the tag-page footer line
- [x] 5.11 Unit tests: `BuildFilterButtons`/`BuildListAsync` with >6 tags — correct tags-per-page slicing, tag-pagination row appears only when `allTags.Count > TagPageSize`, tag page clamps out-of-range values to 1
- [x] 5.12 Unit tests: `ListTagPageCallbackHandler` — valid callback re-renders with new tag page and preserves item page + active filter; malformed callback data is handled without throwing
- [x] 5.13 Unit tests: `ListFilterCallbackHandler` — toggling a tag on tag page 2 preserves tag page 2 in the re-rendered keyboard
- [x] 5.14 Integration test: `/list` in a group with 10 tags — tag page 1 shows tags 1-6 wrapped 2/row plus `[Next →]`; tapping `[Next →]` shows tags 7-10; "All Items"/"Cancel" remain visible on both tag pages
- [x] 5.15 Integration test: tapping item `[Next →]`/`[← Previous]` while tag page 2 is showing resets the re-rendered list to tag page 1

## 6. Documentation

- [x] 6.1 Update the Landing page description of `/list` to mention the smaller action carousel, the always-complete text listing, and tag-list pagination — **N/A**: per `CLAUDE.md`, the Landing page was removed (Telegram bot only); skipped

## 7. Manual Smoke Test (do not mark complete without user confirmation)

- [x] 7.1 In a real Telegram group chat with several items (some categorized) and, ideally, 7+ distinct tags: send `/list`, confirm the action-button carousel pages through items at `ActionPageSize` per page while the message text lists all items; confirm `[← Previous] [n/N] [Next →]` pages correctly; confirm category filter buttons wrap to 2 per row instead of one crowded line; confirm the tag block itself pages through tags 6-at-a-time with its own `[← Previous] [n/N] [Next →]` row, and that "All Items"/"Cancel" stay visible no matter which tag page is showing. Confirmed by user 2026-07-19.

## 8. Post-Implementation Adjustments (same session, after initial smoke test)

Follow-up changes requested after the initial implementation, superseding some Section 1-3 decisions:

- [x] 8.1 Replace the blank no-op spacer rows with an inline page indicator embedded directly in the pagination row: `[← Previous] [n/N] [Next →]` (omitting Previous/Next at the respective ends), applied to both item pagination and tag-page pagination. The `noop` callback prefix and `NoOpCallbackHandler` are retained (now used by the page-indicator button instead of a spacer); the `list.spacer` localization key and `BuildSpacerRow` helper are removed as dead code.
- [x] 8.2 Increase `ActionPageSize` from 2 to 10 per follow-up request — supersedes the original "couple of items" carousel-size decision.
- [x] 8.3 Shorten the `list.remove` localization key from "Remove"/"Удалить"/"Usuń" to "✕" across `Strings.en/ru/pl.json` (Telegram's Bot API has no per-button width control, so this is a label change only, not a true size ratio).
- [x] 8.4 Update unit/integration tests affected by 8.1-8.3.
