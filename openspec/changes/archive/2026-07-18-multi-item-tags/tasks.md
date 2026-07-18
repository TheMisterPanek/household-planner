## 1. Database & Models

- [x] 1.1 Add `Tags` table (`Id INTEGER PRIMARY KEY AUTOINCREMENT`, `GroupId INTEGER NOT NULL`, `Name TEXT NOT NULL`, `UNIQUE (GroupId, Name COLLATE NOCASE)`) via `CREATE TABLE IF NOT EXISTS` in `DatabaseInitializer`
- [x] 1.2 Add `ItemTags` table (`ItemId INTEGER NOT NULL`, `TagId INTEGER NOT NULL`, `PRIMARY KEY (ItemId, TagId)`) via `CREATE TABLE IF NOT EXISTS`
- [x] 1.3 Add `PurchaseHistoryTags` table (`PurchaseHistoryId INTEGER NOT NULL`, `TagId INTEGER NOT NULL`, `PRIMARY KEY (PurchaseHistoryId, TagId)`) via `CREATE TABLE IF NOT EXISTS`
- [x] 1.4 Add one-time backfill in `DatabaseInitializer`, guarded so it runs only when `ItemTags` is empty and at least one `ShoppingItems.Category IS NOT NULL` row exists: for each non-null `ShoppingItems.Category`, get-or-create the group-scoped tag and link via `ItemTags`; same for `PurchaseHistory.Category` → `PurchaseHistoryTags`. Leave `Category` columns unmodified.
- [x] 1.5 Add new `Tag` model (`Id`, `GroupId`, `Name`)
- [x] 1.6 Add `Tags` (`IReadOnlyList<string>`) property to `ShoppingItem` and `PurchaseRecord` models
- [x] 1.7 Add new `TagCaptureDialogState` model: `ItemIds`, `ItemLabel`, `GroupId`, `TopTags` (list of string suggestions), `SelectedTagNames` (mutable set, accumulates toggles/free-text adds across the dialog's lifetime)
- [x] 1.8 Unit tests: backfill migrates existing categories into tags correctly (single item, shared category across two items in one group, runs-once idempotency, `Category` columns left untouched)

## 2. Repository Changes

- [x] 2.1 Add `TagRepository.GetOrCreateAsync(groupId, name)` — case-insensitive lookup-or-insert, returns the tag id
- [x] 2.2 Add `TagRepository.SetItemTagsAsync(itemIds, tagNames)` — bulk replace: for each item id, get-or-create each tag by name, then replace that item's `ItemTags` rows with exactly the resolved set (single item or list, mirrors `UpdateCategoryAsync`'s bulk shape)
- [x] 2.3 Add `TagRepository.GetDistinctTagsAsync(groupId)` — distinct tag names across active items in the group, alphabetical (case-insensitive)
- [x] 2.4 Add `TagRepository.GetTopTagsAsync(groupId, limit)` — top-N tag names from `PurchaseHistoryTags`, ranked by frequency across the whole group, ties broken by most recent linked `PurchaseHistory.PurchasedAt`; truncate labels over 20 chars with "…"
- [x] 2.5 Update `ShoppingItemRepository.AddAsync`/read methods (e.g. `GetAllAsync`) to no longer write `category` and to join `ItemTags`/`Tags` so returned `ShoppingItem`s populate `Tags`
- [x] 2.6 Update `PurchaseHistoryRepository.AddAsync`/read methods to no longer write `category` and to join `PurchaseHistoryTags`/`Tags` so returned `PurchaseRecord`s populate `Tags`
- [x] 2.7 Add `TagRepository.LinkPurchaseHistoryTagsAsync(purchaseHistoryId, tagNames)` — resolves each tag name via `GetOrCreateAsync` and inserts `PurchaseHistoryTags` rows for the given purchase history row
- [x] 2.8 Unit tests: `GetOrCreateAsync` (new tag, existing tag case-insensitive match), `SetItemTagsAsync` (single id, multiple ids, replacing an existing set, clearing to empty), `GetDistinctTagsAsync` (empty, alphabetical, case-insensitivity), `GetTopTagsAsync` (empty, frequency ranking, tie-breaking, truncation), `LinkPurchaseHistoryTagsAsync`, `ShoppingItemRepository`/`PurchaseHistoryRepository` read methods return the correct joined `Tags`

## 3. Tag-Capture Dialog & Handlers

- [x] 3.1 Add `TagCaptureStepHandler` (`IDialogMessageHandler`): `CanHandle` checks for a pending `TagCaptureDialogState`; `HandleAsync` adds the free-text reply to `SelectedTagNames` (does not clear existing selections) and re-renders the prompt message with updated toggle state
- [x] 3.2 Add `TagToggleCallbackHandler` (prefix `tag:toggle:`): resolves the tapped suggestion index against `TagCaptureDialogState.TopTags`, toggles membership in `SelectedTagNames`, re-renders the prompt message (`EditMessageReplyMarkup`) with the updated ✓ state — does not clear the dialog
- [x] 3.3 Add `TagDoneCallbackHandler` (prefix `tag:done`): applies `SelectedTagNames` via `TagRepository.SetItemTagsAsync`, clears the dialog, sends a confirmation listing the applied tags (or a neutral message if the set is empty)
- [x] 3.4 Add `TagSkipCallbackHandler` (prefix `tag:skip`): clears state without applying any tags, edits the prompt message to show it was skipped — mirrors `CategorySkipCallbackHandler`
- [x] 3.5 Add a shared helper (e.g. on `ShoppingListService` or a small new service) `StartTagCaptureAsync(chatId, userId, groupId, itemIds, itemLabel, preselectedTags)` that fetches top tags via `GetTopTagsAsync`, sets `TagCaptureDialogState` (with `SelectedTagNames` pre-populated from `preselectedTags` for the edit-flow re-entry case), and sends the prompt message with toggle buttons (one per row, `tag:toggle:{index}`) plus "Без категории" (`tag:skip`) and "Готово" (`tag:done`) — used by all four trigger points below
- [x] 3.6 Unit tests for `TagCaptureStepHandler`, `TagToggleCallbackHandler`, `TagDoneCallbackHandler`, `TagSkipCallbackHandler`, and the shared start-helper, covering the scenarios in `specs/item-tags/spec.md` (toggle on/off, free-text adds alongside toggles, Done with tags/empty, skip, truncation, pre-selection on edit re-entry)

## 4. Wire the Prompt into Existing /buy and Edit Flows

- [x] 4.1 Replace `CategoryCaptureDialogState`/`CategoryCaptureStepHandler`/`CategorySuggestCallbackHandler`/`CategorySkipCallbackHandler` trigger points in `BuyConfirmCallbackHandler`, `BuySkipCallbackHandler`, `BuyCommandHandler.HandleBulkAddAsync` with calls to the new tag-capture start-helper
- [x] 4.2 `ItemSaveCallbackHandler`: after `UpdateAsync` succeeds, call the tag-capture start-helper for that item, passing its current tags as `preselectedTags` so the re-opened prompt shows existing tags already toggled on
- [x] 4.3 Ensure a new prompt overwrites any still-pending one for the same `(chatId, userId)` (plain `SetState` overwrite, matching existing dialog-abandon behavior)
- [x] 4.4 Unit tests for each updated handler: confirms the follow-up prompt is sent with the right item id(s)/label/pre-selection after the existing confirmation, and that pre-existing confirmation text/timing is unchanged

## 5. Bought Flow — Carry Tags into Purchase History

- [x] 5.1 `ShopDoneCallbackHandler`: read the item's tags before deleting the item, carry them on the `PriceCaptureDialogState` (or an equivalent carrier field)
- [x] 5.2 `PriceCaptureStepHandler.FinishDialogAsync`: after `purchaseRepository.AddAsync` persists the record, call `TagRepository.LinkPurchaseHistoryTagsAsync` with the carried tag set
- [x] 5.3 `PriceSkipCallbackHandler`: same tag-linking call in each of the three skip-branch `PurchaseRecord` constructions
- [x] 5.4 Unit tests: bought item with tags produces `PurchaseHistoryTags` rows for each tag (via full dialog completion and via each skip branch); bought item with no tags produces no `PurchaseHistoryTags` rows

## 6. /list Filtering

- [x] 6.1 Change `ShoppingListService.GetPagedItemsAsync`/`BuildListAsync` from an optional single `category` parameter to an optional `tagNames` set (null/empty = unchanged unfiltered behavior); when set, filter items to those carrying any tag in the set (OR semantics) before paging
- [x] 6.2 Update the filter button row builder: only rendered when `GetDistinctTagsAsync` returns at least one tag; up to 5 alphabetical tags as toggle buttons (`list_filter:<groupChatId>:<tagIndexCsv>:<page>`), each rendered active/inactive based on the currently-active set, plus an "Все" button (`list_filter:<groupChatId>:-1:1`) shown only when at least one tag filter is active
- [x] 6.3 Update `ListCommandHandler` to render the filter row via the updated `BuildListAsync`
- [x] 6.4 Replace `ListFilterCallbackHandler`'s single-category parsing with `<groupChatId>:<tagIndexCsv>:<page>` parsing: toggling an index in/out of the active set, resolving indices against a freshly-queried `GetDistinctTagsAsync` result, dropping any stale index and falling back to the unfiltered "All" view if none remain valid
- [x] 6.5 Ensure filtered-view pagination (next/prev) carries the active tag set through
- [x] 6.6 Unit tests: `BuildListAsync`/`GetPagedItemsAsync` with a single tag filter, with multiple tags (OR union), filter row omitted/shown, active-button rendering, `ListFilterCallbackHandler` (toggle on, toggle off, "Все" tap, stale-tag fallback, pagination within a filtered set)

## 7. Dependency Injection & Wiring

- [x] 7.1 Remove DI registrations for `CategoryCaptureStepHandler`/`CategorySuggestCallbackHandler`/`CategorySkipCallbackHandler`/`PendingDialogService<CategoryCaptureDialogState>`; register `TagCaptureStepHandler` as `IDialogMessageHandler`, `TagToggleCallbackHandler`/`TagDoneCallbackHandler`/`TagSkipCallbackHandler`/updated `ListFilterCallbackHandler` as `ICallbackHandler`, `PendingDialogService<TagCaptureDialogState>` as a singleton, and `TagRepository` as scoped, in `Program.cs`
- [x] 7.2 Wire all new/updated handlers into `TelegramIntegrationTestBase` so they're available to all integration tests

## 8. Localization

- [x] 8.1 Add new localization keys to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`: tag prompt (single + bulk-pluralized), toggle button ✓ prefix format, "Готово" button, "Без категории" skip button, tag-set confirmation (listing applied tags), empty-selection confirmation, filter row's "Все" button
- [x] 8.2 Remove now-unused single-category localization keys added by `product-groups`, once confirmed no code references remain

## 9. Integration Tests

- [x] 9.1 Integration test: `/buy` inline single add → confirmation → tag prompt → toggle two suggestions → "Готово" → item persisted with both tags
- [x] 9.2 Integration test: `/buy` inline single add → tag prompt → free-text reply creates a new tag → toggle a suggestion too → "Готово" → item has both the typed and toggled tags
- [x] 9.3 Integration test: `/buy` bulk add → one tag prompt for the batch → "Готово" → all items get the chosen tag set
- [x] 9.4 Integration test: `/buy` dialog skip-quantity path → tag prompt → skip → item has no tags
- [x] 9.5 Integration test: one-line item edit on an already-tagged item → tag prompt re-fires with existing tags pre-selected → toggle one off, one on → "Готово" → tag set updated accordingly
- [x] 9.6 Integration test: item with tags marked bought → `PurchaseHistory`/`PurchaseHistoryTags` rows carry all the tags
- [x] 9.7 Integration test: `/list` shows no filter row when no active item has a tag
- [x] 9.8 Integration test: `/list` shows filter row when tags exist; tapping one tag filters to items with that tag; tapping a second tag expands the view (OR); tapping an active tag again narrows it; tapping "Все" clears all
- [x] 9.9 Integration test: filtered view pagination with >10 items matching the active tag set
- [x] 9.10 Integration test: tag suggestions are shared across two different users in the same group
- [x] 9.11 Integration test: startup backfill — seed a `ShoppingItems` row with a legacy non-null `Category` directly, start the host, verify a matching `ItemTags` link exists and `Category` is unchanged

## 10. Documentation

- [x] 10.1 N/A — there is no Landing page in this repo (Telegram bot only; the web UI was removed prior to this change, per `CLAUDE.md`). No documentation update needed.

## 11. Manual Smoke Test (do not mark complete without user confirmation)

- [x] 11.1 In a real Telegram group chat: send `/buy Порошок 1кг`, confirm the tag prompt appears with toggle buttons, "Без категории", and "Готово"; tap two suggestions (or, if none exist yet, reply with a new tag name e.g. "Бытовая химия", then reply again with "Скидка" to confirm free text adds without clearing prior selections) and tap "Готово" — confirm both tags are applied; send `/buy Молоко, Яйца` (bulk) and confirm exactly one tag prompt appears for both items, toggle a suggestion, and confirm it's applied to both; run `/buy` with no args through the full dialog (name → quantity → skip button) and confirm the tag prompt appears afterward too; edit an existing tagged item via the one-line edit flow and confirm the prompt re-appears with its current tags pre-selected, then toggle one off and confirm the change sticks; mark a tagged item as bought and verify (via `/search` or DB inspection) that the purchase history entry retains all its tags; send `/list` and verify the tag filter row appears, tap one tag then a second and verify the view expands to items matching either (OR), tap one of the active tags again and verify the view narrows, tap "Все" to return to the full list. Confirmed via real-chat usage (free-text tag capture, ack flow, and the list-refresh fix that followed).
