## 1. Carousel Page Size

- [ ] 1.1 Extract a `private const int ActionPageSize = 2;` in `ShoppingListService`, replacing the hardcoded `const int pageSize = 10;` local in `BuildListAsync`
- [ ] 1.2 Verify `FormatItemsAsText(allItems)` continues to render the full, unpaginated item set regardless of `ActionPageSize` (no code change expected — confirm with a test)
- [ ] 1.3 Unit tests: `GetPagedItemsAsync`/`BuildListAsync` with `ActionPageSize = 2` — correct item count per page, correct total-pages calculation, footer text reflects the new page size

## 2. No-Op Spacer Button

- [ ] 2.1 Add `list.spacer` localization key (short neutral label, e.g. "▫️▫️▫️") to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`
- [ ] 2.2 Add `NoOpCallbackHandler` (`ICallbackHandler`, prefix `noop`): answers the callback query, takes no other action
- [ ] 2.3 Register `NoOpCallbackHandler` in `Program.cs` and wire it into `TelegramIntegrationTestBase`
- [ ] 2.4 Unit tests: `NoOpCallbackHandler` answers the callback and performs no repository/state calls

## 3. Keyboard Layout Changes

- [ ] 3.1 In `BuildListAsync`, chunk the category filter buttons into rows of at most 2 (`.Chunk(2)`) instead of a single row
- [ ] 3.2 Insert a spacer row (using the `noop` callback and `list.spacer` label) between the item action-button rows and the pagination row, only when a pagination row is rendered
- [ ] 3.3 Insert a spacer row between the pagination row (or, if absent, the item action-button rows) and the filter row(s), only when at least one filter row is rendered
- [ ] 3.4 Ensure no spacer rows are inserted when neither a pagination row nor a filter row follows (list has ≤ `ActionPageSize` items and no categorized items)
- [ ] 3.5 Unit tests: keyboard structure for (a) short list, no pagination, no filters — no spacers; (b) pagination present, no filters — one spacer; (c) no pagination, filters present — one spacer; (d) both present — two spacers; (e) filter row wrapping with 6 total filter buttons renders as 3 rows of 2

## 4. Integration Tests

- [ ] 4.1 Integration test: `/list` with 5 items — first carousel page shows exactly 2 action rows, text body lists all 5 items, `[Next →]` present
- [ ] 4.2 Integration test: tapping `[Next →]`/`[← Previous]` pages through the carousel correctly at the new page size
- [ ] 4.3 Integration test: `/list` with categorized items and multiple carousel pages — spacer rows appear in the expected positions and filter buttons render wrapped
- [ ] 4.4 Integration test: tapping a spacer button — no error, no state change, list message unchanged

## 5. Documentation

- [ ] 5.1 Update the Landing page description of `/list` to mention the smaller action carousel and the always-complete text listing

## 6. Manual Smoke Test (do not mark complete without user confirmation)

- [ ] 6.1 In a real Telegram group chat with 5+ items (some categorized): send `/list`, confirm only 2 items show as action-button rows while the message text lists all items; confirm `[Next →]`/`[← Previous]` page through 2 at a time; confirm category filter buttons wrap to 2 per row instead of one crowded line; confirm a blank spacer row visually separates the item buttons from pagination, and pagination/items from the filter row; tap the spacer and confirm nothing happens (no error, no change). **Leave this task open until the user confirms the smoke test passed.**
