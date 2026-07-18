## Why

The just-shipped `product-groups` change gave each shopping item a single optional `Category` string — explicitly a "one tag, not a tag set" design (see `openspec/changes/product-groups/design.md`, Non-Goals). In practice, users want to file an item under more than one label at once (e.g. "Молочное" *and* "Скидка", or "Бытовая химия" *and* "Для кухни") and filter `/list` by any of them. A single mutually-exclusive category can't express that — this change replaces the one-category-per-item model with true many-to-many tags.

## What Changes

- Add a `Tags` table (id, group-scoped name, case-insensitive unique per group) and an `ItemTags` join table (many-to-many between `ShoppingItems` and `Tags`).
- **BREAKING** (data model only, not user-facing): the existing nullable `ShoppingItems.Category` / `PurchaseHistory.Category` string columns are superseded by the tag tables. Both columns are left in place (additive-only migrations — no dropping columns) but stop being written to by new code once this change ships; a one-time backfill migrates each non-null `Category` value into a tag and links it to the owning item/history row, so existing categorization isn't lost.
- The category-capture follow-up prompt (after `/buy` add or item edit) becomes multi-select: suggestion buttons toggle on/off (checkmark shown when selected) instead of immediately resolving the prompt; a new "Готово" (Done) button confirms the current selection; "Без категории" clears/skips as today. A free-text reply still creates a new tag, and can add it in addition to any already-toggled suggestions rather than replacing them.
- `PurchaseHistory` gains the same `ItemTags`-style link (a `PurchaseHistoryTags` join table) so tag suggestions keep working after items are bought and removed from the active list, mirroring how `product-groups` sourced suggestions from `PurchaseHistory` rather than the live table.
- `/list` category filter becomes a tag filter: tapping a tag shows items carrying that tag (an item can appear under multiple tags); multiple tags can be active at once (item shown if it has ANY of the active tags — OR semantics, not AND), with "Все" clearing all active filters.
- Tag suggestion ranking stays group-wide and frequency-ranked, same as `product-groups` Decision 3, just sourced from the join tables instead of a single column.

## Capabilities

### New Capabilities
- `item-tags`: tag storage (`Tags`, `ItemTags`, `PurchaseHistoryTags`), the multi-select tag-capture follow-up dialog (toggleable suggestions + free-text-adds-a-tag + Done/skip), group-wide frequency ranking sourced from purchase history, and the multi-tag filtered `/list` view. Supersedes the `product-tags` capability introduced by the (not yet archived) `product-groups` change.

### Modified Capabilities
- `shopping-list`: `/list` gains multi-tag filter buttons (OR-semantics, multiple active at once) in place of the single-category filter row; the underlying paging/rendering behavior for the unfiltered view is unchanged.

## Impact

- **Database**: two new tables, `Tags` (`Id`, `GroupId`, `Name`, unique on `(GroupId, lower(Name))`) and `ItemTags` (`ItemId`, `TagId`, composite PK), plus `PurchaseHistoryTags` (`PurchaseHistoryId`, `TagId`). All created via `CREATE TABLE IF NOT EXISTS` in `DatabaseInitializer`, per the existing additive-migration pattern. `ShoppingItems.Category` / `PurchaseHistory.Category` columns remain (read-only, for the one-time backfill and as a historical fallback) but are no longer written by new code.
- **Models**: new `Tag` model; `ShoppingItem`/`PurchaseRecord` gain a `Tags` (`IReadOnlyList<string>`) property populated via a join query; new `TagCaptureDialogState` (mirrors `CategoryCaptureDialogState` but carries a mutable in-progress selection set, not a single resolved value).
- **Repositories**: new `TagRepository` — `GetOrCreateAsync(groupId, name)`, `SetItemTagsAsync(itemIds, tagIds)` (bulk replace), `GetDistinctTagsAsync(groupId)`, `GetTopTagsAsync(groupId, limit)` (frequency-ranked from `PurchaseHistoryTags`). `ShoppingItemRepository`/`PurchaseHistoryRepository` gain joined tag-fetching in their existing read methods (`GetAllAsync`, etc.) and lose their `category` write parameters.
- **Services**: `ShoppingListService` — filtering changes from a single `category` parameter to a `tagIds` set (OR-matched); tag filter button row replaces the category filter row.
- **Handlers**: `CategoryCaptureStepHandler`/`CategorySuggestCallbackHandler`/`CategorySkipCallbackHandler`/`ListFilterCallbackHandler` are replaced by tag-aware equivalents (`TagCaptureStepHandler`, `TagToggleCallbackHandler`, `TagDoneCallbackHandler`, `TagSkipCallbackHandler`, `ListTagFilterCallbackHandler`); the four trigger points (`BuyConfirmCallbackHandler`, `BuySkipCallbackHandler`, `BuyCommandHandler.HandleBulkAddAsync`, `ItemSaveCallbackHandler`) are updated to start `TagCaptureDialogState` instead of `CategoryCaptureDialogState`.
- **Localization**: new keys for the multi-select prompt, tag chip toggle state, "Готово" button, and multi-tag filter row; existing single-category keys are retired.
- **Rollback plan**: new tables are additive; the superseded `Category` columns are untouched, so reverting the code deploy leaves data intact — the old single-category code path would resume reading the (now possibly stale, since it stopped being written) `Category` column. Because that's a lossy rollback path (tags added after this ships wouldn't reappear as a `Category`), the rollback plan explicitly calls out re-running the backfill in reverse is *not* supported — rollback is safe for data-loss purposes but not fully symmetric.
- **AOT compatibility**: no reflection-based JSON or DI patterns introduced; tags are plain relational rows, not JSON payloads.
- **Affected teams**: none beyond the bot maintainer(s); no external API or web UI changes (Web UI is currently disabled).
- **Cross-cutting decisions relied on**: SQLite raw ADO additive migration (`CREATE TABLE IF NOT EXISTS`, no migration framework); Telegram dispatch via existing `ICallbackHandler`/`ICommandHandler`/dialog trio, extended with toggle-state callback data (`tag:toggle:<index>`, `tag:done`, `tag:skip`) that stays within the 64-byte limit the same way `product-groups`' `category:suggest:<index>` did; all new user-facing strings go through `ILocalizer`.
- **Dependency note**: this change modifies a capability (`product-tags`) introduced by `openspec/changes/product-groups/`, which has not yet been archived (its manual smoke-test task is still open). If `product-groups` is archived before this change is implemented, update this proposal's "Modified/Superseded Capabilities" references from the change folder to `openspec/specs/product-tags/`.
