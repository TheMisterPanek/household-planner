## Why

The `/list` message's inline keyboard has grown cramped: the category filter row packs up to 6 buttons into a single line, causing label truncation/overlap on narrow screens (observed directly — see the reported screenshot). Separately, the action-button page (10 items) duplicates information already fully present in the message's plain-text item listing (`FormatItemsAsText` already renders every item as text regardless of the button page), so there's no reason for the interactive button page to hold that many rows. This change reworks the `/list` keyboard into a tighter, clearer layout: a "carousel" of action buttons, category filter buttons wrapped two per row instead of one crowded row, and pagination rows that carry their own inline page indicator instead of relying on separate blank rows for visual separation.

**Note:** this proposal originally shipped with a 2-item carousel page and blank spacer rows between keyboard sections. Both were revised in a same-session follow-up after live use: the page size was raised to 10 (still smaller than the original 10-item default's *lack* of paging, but not as aggressive as 2), and the spacer rows were replaced by an inline `[n/N]` page indicator embedded in the pagination row itself. See `design.md`'s Post-Implementation Adjustments / superseded decisions for detail. The `What Changes`/`Impact` sections below describe the as-shipped behavior.

## What Changes

- Reduce the action-button page size (the `pageSize` used for `GetPagedItemsAsync`/pagination in `BuildListAsync`) from the old hardcoded 10 to a smaller, dedicated `ActionPageSize` constant (shipped at 10 after a same-session revision from an initial value of 2) — pagination (`[← Previous] [n/N] [Next →]`) now pages through the interactive button rows. The plain-text item listing in the message body is unaffected — it continues to list every item, unpaginated, exactly as today.
- Wrap the category filter button row into rows of at most 2 buttons each, instead of a single row holding up to 6.
- Remove the hard 5-tag display cap on filter buttons and replace it with pagination over the tag list itself (`TagPageSize = 6`, i.e. 3 wrapped rows per tag page), with its own `[← Previous] [n/N] [Next →]` row and a new `list_tagpage` callback/handler — today, a group with more than 5 distinct tags has no way to reach tag 6+ at all.
- "All Items" (clear filter) and "Cancel" render as fixed rows below the tag-page block, always visible regardless of which tag page is showing.
- Both the item-pagination row and the tag-pagination row render an inline page indicator (`[n/N]`, using the `noop` callback) between the Previous/Next buttons, giving visual context without a dedicated blank row.
- Add a `NoOpCallbackHandler` (callback prefix `noop`) that answers the callback query and does nothing else — used by the page-indicator button.
- The item action row's Remove button label was shortened to "✕" across `en`/`ru`/`pl` locales.
- **BREAKING**: none — purely a rendering/layout change; no data model or command syntax changes. Existing `/list <page>` behavior is unchanged except that "a page" now means the `ActionPageSize` batch of action buttons; the footer's "Page X of Y (Z items)" continues to reflect the action-page pagination, not the full text listing. Groups with more than 5 tags gain the ability to reach previously-unreachable tags — a strict improvement, not a behavior change to existing reachable tags.

## Capabilities

### Modified Capabilities
- `shopping-list`: the `/list` inline keyboard layout changes — smaller action-button pages, wrapped and now-paginated filter rows, an inline page indicator on both pagination rows instead of separate spacer rows.
- `list-pagination`: the fixed 10-items-per-page constant becomes a dedicated `ActionPageSize` constant; navigation button behavior (Previous/Next appearance rules) is otherwise unchanged, with an added `[n/N]` indicator button. A second, independent pagination dimension (tag pages) is introduced for the filter block.

## Impact

- **Services**: `ShoppingListService.BuildListAsync`/`GetPagedItemsAsync` — change the `pageSize` constant, wrap filter-button construction into chunks of 2, paginate the tag list (`TagPageSize = 6`), render `[n/N]` page indicators inline in both pagination rows.
- **Handlers**: new `NoOpCallbackHandler` (prefix `noop`) and new `ListTagPageCallbackHandler` (prefix `list_tagpage`), both registered as `ICallbackHandler` in `Program.cs` and wired into `TelegramIntegrationTestBase`. `ListFilterCallbackHandler`'s callback-data parsing gains a 4th field (tag page number).
- **Localization**: a new `list.tags-label` key ("Tags") is added for the tag-page footer line. The `list.remove` key was shortened to "✕" across locales. (The originally-added `list.spacer` key was removed again after the spacer-row design was superseded — see `design.md`.)
- **Rollback plan**: revert the page-size constant and keyboard-building changes; no schema or persisted-data changes are introduced, so rollback is a plain code revert.
- **AOT compatibility**: no reflection or new serialization; plain keyboard-building logic.
- **Affected teams**: none beyond the bot maintainer(s).
- **Cross-cutting decisions relied on**: Telegram dispatch via the existing `ICallbackHandler` trio (no new routing mechanism); callback data for the page indicator (`noop`) and tag pagination (`list_tagpage`) trivially fit the 64-byte limit for realistic tag counts; all new user-facing strings go through `ILocalizer`.
