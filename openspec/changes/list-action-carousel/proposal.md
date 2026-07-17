## Why

The `/list` message's inline keyboard has grown cramped: the category filter row packs up to 6 buttons into a single line, causing label truncation/overlap on narrow screens (observed directly — see the reported screenshot). Separately, the action-button page (10 items) duplicates information already fully present in the message's plain-text item listing (`FormatItemsAsText` already renders every item as text regardless of the button page), so there's no reason for the interactive button page to hold that many rows — a shopper handles a couple of items at a time in-store, not ten. This change reworks the `/list` keyboard into a tighter, clearer layout: a small "carousel" of action buttons (a couple of items per page), category filter buttons wrapped two per row instead of one crowded row, and blank non-interactive spacer rows marking the boundary between the item-action block, the pagination controls, and the filter block.

## What Changes

- Reduce the action-button page size (the `pageSize` used for `GetPagedItemsAsync`/pagination in `BuildListAsync`) from 10 to a small carousel size (e.g. 2 items per page) — pagination (`[← Previous]`/`[Next →]`) now pages through the interactive button rows in small increments. The plain-text item listing in the message body is unaffected — it continues to list every item, unpaginated, exactly as today.
- Wrap the category filter button row into rows of at most 2 buttons each, instead of a single row holding up to 6.
- Insert a non-interactive, single "spacer" button (full-width, does nothing when tapped — answers the callback query with no state change) between: (a) the block of item action-button rows, (b) the pagination row, and (c) the filter button row(s). No spacer is inserted where a block is absent (e.g. no spacer before the filter block if there are no pagination buttons to separate it from).
- Add a `NoOpCallbackHandler` (callback prefix `noop`) that answers the callback query and does nothing else — used exclusively by the spacer button.
- **BREAKING**: none — purely a rendering/layout change; no data model or command syntax changes. Existing `/list <page>` behavior is unchanged except that "a page" now means a smaller batch of action buttons; the footer's "Page X of Y (Z items)" continues to reflect the (now smaller) action-page pagination, not the full text listing.

## Capabilities

### Modified Capabilities
- `shopping-list`: the `/list` inline keyboard layout changes — smaller action-button pages, wrapped filter rows, spacer rows between sections.
- `list-pagination`: the fixed 10-items-per-page constant becomes a smaller carousel page size; navigation button behavior (Previous/Next appearance rules) is otherwise unchanged.

## Impact

- **Services**: `ShoppingListService.BuildListAsync`/`GetPagedItemsAsync` — change the `pageSize` constant, wrap filter-button construction into chunks of 2, insert spacer rows at the three section boundaries.
- **Handlers**: new `NoOpCallbackHandler` (prefix `noop`), registered as `ICallbackHandler` in `Program.cs` and wired into `TelegramIntegrationTestBase`.
- **Localization**: no new user-facing text is needed for the spacer (empty or a neutral non-informative label, e.g. an em-dash "—", via a new `list.spacer` key so it isn't a hardcoded literal).
- **Rollback plan**: revert the page-size constant and keyboard-building changes; no schema or persisted-data changes are introduced, so rollback is a plain code revert.
- **AOT compatibility**: no reflection or new serialization; plain keyboard-building logic.
- **Affected teams**: none beyond the bot maintainer(s).
- **Cross-cutting decisions relied on**: Telegram dispatch via the existing `ICallbackHandler` trio (no new routing mechanism); callback data for the spacer (`noop`) trivially fits the 64-byte limit; all new user-facing strings (the spacer label) go through `ILocalizer`.
- **Dependency note**: this change touches the same `BuildListAsync` method that `openspec/changes/multi-item-tags/` (not yet implemented) plans to modify for tag-based filtering. If both changes are implemented, apply this one's page-size/spacer/wrapping changes first (or rebase `multi-item-tags`'s filter-row logic on top of it) to avoid duplicate keyboard-construction rework.
