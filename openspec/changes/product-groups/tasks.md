## 1. Database & Models

- [x] 1.1 Add `Category TEXT NULL` column migration for `ShoppingItems` in `DatabaseInitializer` (PRAGMA table_info check + ALTER TABLE, matching the `exp_date` pattern)
- [x] 1.2 Add `Category TEXT NULL` column migration for `PurchaseHistory` in `DatabaseInitializer`, same pattern
- [x] 1.3 Add `Category` property to `ShoppingItem` model
- [x] 1.4 Add `Category` property to `PurchaseRecord` model
- [x] 1.5 Add `Category` property to `PriceCaptureDialogState` model (carries the item's category through to the purchase record when marked bought)
- [x] 1.6 Add new `CategoryCaptureDialogState` model: `ItemIds` (list of int), `ItemLabel` (display text for the prompt — item name, or "N товаров" for bulk), `GroupId`, `TopCategories` (list of string, suggestions for the current prompt)

## 2. Repository Changes

- [x] 2.1 Update `ShoppingItemRepository.AddAsync` to accept and persist an optional `category` parameter (defaults to null, existing call sites unaffected)
- [x] 2.2 Add `ShoppingItemRepository.UpdateCategoryAsync(IReadOnlyList<int> itemIds, string? category)` — bulk update, single query
- [x] 2.3 Add `ShoppingItemRepository.GetDistinctCategoriesAsync(groupId)` — distinct non-null `Category` values across active items in the group, alphabetical (case-insensitive); used by `/list` filtering
- [x] 2.4 Update `PurchaseHistoryRepository.AddAsync` to persist `PurchaseRecord.Category`
- [x] 2.5 Add `PurchaseHistoryRepository.GetTopCategoriesAsync(groupId, limit)` — top-N distinct `Category` values from `PurchaseHistory`, ranked by frequency across the whole group (not per user, unlike `GetTopShopsAsync`), ties broken by most recent `PurchasedAt`; truncate labels over 20 chars with "…" (mirrors `GetTopShopsAsync`'s 30-char truncation, but shorter since these buttons share a row with others)
- [x] 2.6 Unit tests: `AddAsync` with category, `UpdateCategoryAsync` (single id, multiple ids, clearing with null), `GetDistinctCategoriesAsync` (empty, alphabetical ordering, case-insensitivity), `GetTopCategoriesAsync` (empty, frequency ranking, tie-breaking by recency, truncation), `PurchaseHistoryRepository.AddAsync` persists category

## 3. Category-Capture Dialog & Handlers

- [x] 3.1 Add `CategoryCaptureStepHandler` (`IDialogMessageHandler`): `CanHandle` checks for a pending `CategoryCaptureDialogState`; `HandleAsync` applies the free-text reply as the category via `UpdateCategoryAsync`, clears state, sends a confirmation (e.g. "✓ Категория «X» установлена")
- [x] 3.2 Add `CategorySuggestCallbackHandler` (prefix `category:suggest:`): resolves the tapped suggestion index against `CategoryCaptureDialogState.TopCategories`, applies it via `UpdateCategoryAsync`, clears state, confirms — mirrors `PriceShopSuggestionCallbackHandler`
- [x] 3.3 Add `CategorySkipCallbackHandler` (prefix `category:skip`): clears state without setting a category, edits the prompt message to show it was skipped — mirrors `PriceSkipCallbackHandler`'s step-1 skip branch
- [x] 3.4 Add a shared helper (e.g. on `ShoppingListService` or a small new service) `StartCategoryCaptureAsync(chatId, userId, groupId, itemIds, itemLabel)` that fetches top categories via `GetTopCategoriesAsync`, sets `CategoryCaptureDialogState`, and sends the prompt message with suggestion buttons (one per row, `category:suggest:{index}`) plus the skip button (`category:skip`) — used by all four trigger points below
- [x] 3.5 Unit tests for `CategoryCaptureStepHandler`, `CategorySuggestCallbackHandler`, `CategorySkipCallbackHandler`, and the shared start-helper, covering the scenarios in `specs/product-tags/spec.md` (suggestions present/absent, tap, free-text/new category, skip, truncation)

## 4. Wire the Prompt into Existing /buy and Edit Flows

- [x] 4.1 `BuyConfirmCallbackHandler`: after `AddAsync` and the confirmation message, call the start-category-capture helper for the single new item
- [x] 4.2 `BuySkipCallbackHandler`: after `AddAsync` and the confirmation message, call the start-category-capture helper for the single new item
- [x] 4.3 `BuyCommandHandler.HandleBulkAddAsync`: after all items are added and the bulk confirmation is sent, call the start-category-capture helper once with all newly-added item IDs and a pluralized label
- [x] 4.4 `ItemSaveCallbackHandler`: after `UpdateAsync` (name/quantity) succeeds, call the start-category-capture helper for that item, so editing re-opens the category prompt
- [x] 4.5 Ensure a new prompt overwrites any still-pending one for the same `(chatId, userId)` (plain `SetState` overwrite — matches the existing `PriceCaptureDialogState` abandon behavior, no extra guard needed)
- [x] 4.6 Unit tests for each updated handler: confirms the follow-up prompt is sent with the right item id(s)/label after the existing confirmation, and that pre-existing confirmation text/timing is unchanged

## 5. Bought Flow — Carry Category into Purchase History

- [x] 5.1 `ShopDoneCallbackHandler`: read `item.Category` before deleting the item, set it on the new `PriceCaptureDialogState`
- [x] 5.2 `PriceCaptureStepHandler.FinishDialogAsync`: set `record.Category = state.Category` on the `PurchaseRecord` before calling `purchaseRepository.AddAsync`
- [x] 5.3 `PriceSkipCallbackHandler`: set `record.Category = state.Category` in each of the three skip-branch `PurchaseRecord` constructions (step 1/2/3 skip paths)
- [x] 5.4 Unit tests: bought item with a category produces a `PurchaseRecord` with that category (via full dialog completion and via each skip branch); bought item with no category produces `Category = null`

## 6. /list Filtering

- [x] 6.1 Add optional `category` parameter to `ShoppingListService.GetPagedItemsAsync` and `BuildListAsync` (null = unchanged/unfiltered behavior); when set, filter items by `Category` (case-insensitive exact match) before paging
- [x] 6.2 Add category filter button row builder to `ShoppingListService`: only rendered when `GetDistinctCategoriesAsync` returns at least one category; up to 5 alphabetical categories as buttons (`list_filter:<groupChatId>:<categoryIndex>:<page>`), plus an "Все" button (`list_filter:<groupChatId>:-1:1`) shown only when a filter is currently active
- [x] 6.3 Update `ListCommandHandler` to render the filter row via the updated `BuildListAsync`
- [x] 6.4 Add `ListFilterCallbackHandler` (prefix `list_filter:`): parses `<groupChatId>:<categoryIndex>:<page>`, resolves `categoryIndex` against a freshly-queried `GetDistinctCategoriesAsync` result, falling back to the unfiltered "All" view if the index no longer resolves (per the "Category no longer exists at tap time" scenario)
- [x] 6.5 Ensure filtered-view pagination (next/prev) carries the active category filter through
- [x] 6.6 Unit tests: `BuildListAsync`/`GetPagedItemsAsync` with a category filter, filter row omitted/shown, `ListFilterCallbackHandler` (valid tap, "Все" tap, stale category fallback, pagination within a filtered category)

## 7. Dependency Injection & Wiring

- [x] 7.1 Register `CategoryCaptureStepHandler` as `IDialogMessageHandler`, `CategorySuggestCallbackHandler`/`CategorySkipCallbackHandler`/`ListFilterCallbackHandler` as `ICallbackHandler`, and `PendingDialogService<CategoryCaptureDialogState>` as a singleton, in `Program.cs`
- [x] 7.2 Wire all new handlers into `TelegramIntegrationTestBase` so they're available to all integration tests

## 8. Localization

- [x] 8.1 Add new localization keys to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`: category prompt (single + bulk-pluralized), "Без категории" skip button, category-set confirmation, filter row's "Все" button

## 9. Integration Tests

- [x] 9.1 Integration test: `/buy` inline single add → confirmation → category prompt → tap suggestion → item persisted with category
- [x] 9.2 Integration test: `/buy` inline single add → category prompt → free-text reply creates a new category
- [x] 9.3 Integration test: `/buy` bulk add → one category prompt for the batch → all items get the chosen category
- [x] 9.4 Integration test: `/buy` dialog skip-quantity path → category prompt → skip → item has no category
- [x] 9.5 Integration test: one-line item edit → category prompt fires → category changed
- [x] 9.6 Integration test: item with a category marked bought → `PurchaseHistory` row carries the category
- [x] 9.7 Integration test: `/list` shows no filter row when no active item has a category
- [x] 9.8 Integration test: `/list` shows filter row when categories exist; tapping a category filters the view; tapping "Все" clears it
- [x] 9.9 Integration test: filtered view pagination with >10 items in one category
- [x] 9.10 Integration test: category suggestions are shared across two different users in the same group

## 10. Documentation

- [x] 10.1 Update the Landing page with a description of the product categories/tags feature (category prompt after adding/editing an item, suggestions, creating a new category by replying, filtering `/list` by category)

## 11. Manual Smoke Test (do not mark complete without user confirmation)

- [ ] 11.1 In a real Telegram group chat: send `/buy Порошок 1кг`, confirm the bot sends the item-added confirmation followed by a category prompt with a "Без категории" button (and no suggestions yet, if this is the first categorized item); reply with a new category name (e.g. "Бытовая химия") and confirm it's applied; send `/buy Молоко, Яйца` (bulk) and confirm exactly one category prompt appears for both items, and tapping a suggestion (should now include "Бытовая химия") applies it to both; run `/buy` with no args through the full dialog (name → quantity → skip button) and confirm the category prompt appears afterward too; edit an existing item via the one-line edit flow and confirm the category prompt re-appears and can change the category; mark a categorized item as bought and verify (via `/search` or DB inspection) that the purchase history entry retains the category; send `/list` and verify the category filter row appears with the right categories, tap a category and verify only matching items show, tap "Все" to return to the full list. **Leave this task open until the user confirms the smoke test passed.**
