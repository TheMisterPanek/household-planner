## Context

`ShoppingItems` today has no notion of category — every item is a flat row (`Id`, `GroupId`, `Name`, `Quantity`, `ExpDate`, `AddedByName`). `/list` renders all items for a group as a single paginated list (`ShoppingListService.BuildListAsync`, page size 10, see `list-pagination` spec). There is no product catalog — items are free-text per add, so the same product name can appear with different text each time. Categories must therefore live on the `ShoppingItems` row itself, not on a separate "product" master record.

The codebase already has an exact precedent for the interaction this change needs: when a user taps ✓ to mark an item bought (`ShopDoneCallbackHandler`), the item is deleted immediately, a confirmation is sent, and then — as a separate follow-up message — the bot starts a `PriceCaptureDialogState` asking "📍 Where did you buy {item}?" with inline buttons for the user's top 5 shops (from `PurchaseHistory`, via `PurchaseHistoryRepository.GetTopShopsAsync`) plus a `[Skip]` button (`price:skip_store`). Typing free text instead of tapping a button sets a custom store name (`PriceCaptureStepHandler.HandleStep1Async`). The user explicitly asked for this same shape for categories: the bot should ask which group/category to file an item under right after adding it, with suggestions, a skip option, and free-text-reply-creates-a-new-category. This design reuses that pattern directly — a new `CategoryCaptureDialogState`, `CategorySuggestCallbackHandler`, `CategorySkipCallbackHandler`, and `CategoryCaptureStepHandler` mirror `PriceCaptureDialogState`/`PriceShopSuggestionCallbackHandler`/`PriceSkipCallbackHandler`/`PriceCaptureStepHandler` almost one-for-one.

## Goals / Non-Goals

**Goals:**
- After every `/buy` add (and after editing an item), ask the user which category to file it under, with suggestions so they rarely have to type a new one.
- Let a user create a brand-new category on the spot simply by replying with free text instead of picking a suggestion.
- Let a user filter the `/list` view down to a single category to speed up in-store navigation.
- Keep suggestions useful over time — they must not disappear just because the tagged items were bought and removed from the active list.
- Keep the default (unfiltered) `/list` rendering and pagination behavior unchanged, to minimize risk to the existing, well-covered `shopping-list` spec.

**Non-Goals:**
- No fixed/predefined taxonomy of categories — free text only, no category management UI (rename/delete a category as a standalone entity; a user "renames" one by re-editing tagged items).
- No multi-category tagging per item (one optional category per item, not a tag *set*).
- No category-based grouping/headers inside the default paginated list — grouping only happens via the explicit filter view (see Decisions).
- No category capture for `/bought` (a direct manual purchase log entry with no antecedent shopping-list item to carry a category from) — out of scope for this change.
- No changes to `/search`, `meal-shopping-merge`, or the (currently disabled) Web UI.
- No retroactive auto-categorization of existing items.

## Decisions

### 1. Category is a nullable string column on `ShoppingItems`, not a separate table
Alternative considered: a `Categories` table with a `CategoryId` FK, enabling rename-in-place and stable identity. Rejected for v1: it adds a migration, a repository, and referential-integrity handling for zero clear benefit, since categories are per-item free text a user can already "rename" by re-editing each item (rare — grocery lists are small). If usage shows people want a shared, renameable taxonomy, that's a follow-up change.

Column: `ShoppingItems.Category TEXT NULL`, added via the standard `PRAGMA table_info` + `ALTER TABLE ShoppingItems ADD COLUMN Category TEXT NULL` pattern in `DatabaseInitializer`, mirroring the existing `exp_date` migration.

### 2. Category is captured by a follow-up prompt after the item is persisted, not a parsing shortcut or an extra step inside the add flow itself
Alternative considered: a trailing `#tag` token parsed out of the `/buy` text (e.g. `/buy Порошок 1кг #Бытовая химия`). Rejected per explicit user feedback — typing a tag by hand is exactly the retyping friction the suggestion-button UX (see Decision 3) is meant to avoid, and it's inconsistent with how every other optional attribute in this codebase (store, price, expiry) is captured: as a follow-up question after the core save, not embedded in the initial command text.

Alternative considered: insert a mandatory "Category?" question as a new step *inside* the existing `/buy` dialog/review flow (before the item is saved). Rejected: every `/buy` path — inline single, bulk, and the two-step dialog — currently converges on the same review step (`buy.review-add*` message with Confirm/Edit/Cancel buttons, `BuyConfirmCallbackHandler`) or the skip-quantity path (`BuySkipCallbackHandler`), both of which call `ShoppingItemRepository.AddAsync` directly. Gating the save on an answer would require restructuring all of those paths and would delay the "item added" confirmation the user currently gets immediately.

Decision: keep every existing `/buy` path's save timing exactly as today. Immediately **after** `ShoppingItemRepository.AddAsync` succeeds and the existing "Иван добавил(а) X" confirmation is sent, start a `CategoryCaptureDialogState` and send a separate follow-up message — the same shape as `ShopDoneCallbackHandler` starting `PriceCaptureDialogState` right after deleting a bought item. Concretely, this follow-up is started from the four places an item gets persisted:
- `BuyConfirmCallbackHandler` (inline single add and the dialog/one-line-mode review-confirm path — these already converge here)
- `BuySkipCallbackHandler` (dialog skip-quantity path)
- `BuyCommandHandler.HandleBulkAddAsync` (bulk add — started **once** for the whole batch, not once per item; see Decision 3a)
- `ItemSaveCallbackHandler` (one-line edit save — re-prompts so category can be added/changed/cleared after an edit)

The prompt: `"category.prompt"` → "В какую группу добавить {item}?" with inline buttons for suggested categories (Decision 3) plus a `"Без категории"` skip button (`category:skip`). A free-text reply is handled by `CategoryCaptureStepHandler` (`IDialogMessageHandler`), which sets the category to whatever was typed — this is the "create a new category by replying" mechanism the user asked for, identical in shape to typing a custom shop name instead of tapping a suggested one in step 1 of price capture.

If a new category-capture prompt starts while a previous one for the same `(chatId, userId)` is still pending (e.g., user adds a second item before answering the first prompt), the new `SetState` call simply overwrites the old one — the same "abandon" behavior already accepted for `PriceCaptureDialogState` (see the existing "User abandons price-capture dialog" scenario).

### 3. Category suggestions are ranked per group (not per user) and sourced from `PurchaseHistory` (not the live `ShoppingItems` table)
Two deviations from the `shop-suggestions` precedent, each deliberate:
- **Per group, not per user.** `GetTopShopsAsync` is scoped to `(groupId, userId)` because which shop *you personally* visit is a personal preference. A category/tag taxonomy is the opposite — it's a shared organizing scheme for the group's list, and everyone should see and reuse the same "Бытовая химия" tag regardless of who typed it first. `PurchaseHistoryRepository.GetTopCategoriesAsync(groupId, limit)` is therefore scoped to `groupId` only, ranked by frequency of use across all members, ties broken by most recent use — otherwise identical shape to `GetTopShopsAsync`.
- **Sourced from `PurchaseHistory`, not `ShoppingItems`.** `ShoppingItems` rows are deleted the moment an item is marked bought or removed (`ShopDoneCallbackHandler`/`ShopRemoveCallbackHandler`), so a suggestion pool drawn from the live table would shrink toward empty as soon as tagged items are checked off — defeating the purpose of suggesting frequently-used categories. Since `PurchaseHistory` is the permanent log of completed purchases, `PurchaseRecord` gains a `Category` column, populated by carrying over the `ShoppingItem.Category` value (already set via the follow-up prompt, by the time the item is later bought) through `PriceCaptureDialogState.Category` into the saved `PurchaseRecord` in both `PriceCaptureStepHandler.FinishDialogAsync` and the skip-branches of `PriceSkipCallbackHandler`. This is exactly how `StoreName`/`Price` already flow from dialog state into the persisted record.

#### 3a. Bulk add asks once for the whole batch
Alternative considered: prompt once per item added via bulk `/buy`. Rejected — bulk add exists specifically to add many items in one message with minimal friction; N sequential category prompts would be worse UX than the flat list it replaced. Instead, `CategoryCaptureDialogState` carries a list of item IDs (`ItemIds`); the prompt text pluralizes ("В какую группу добавить эти {count} товара(ов)?"); on resolution (button tap or free-text reply), the chosen category is applied to all IDs in one call to a new `ShoppingItemRepository.UpdateCategoryAsync(itemIds, category)`.

### 4. List filtering uses a server-resolved category index, not the category text, in callback data
Telegram callback data must stay within 64 bytes. Category text plus a group chat ID could exceed that for long tags. Alternative considered: an in-memory session token (per the cross-cutting guidance for oversized payloads). Rejected as overkill: the set of categories for a group is small and cheap to recompute. Instead, callback data is `list_filter:<groupChatId>:<categoryIndex>:<page>`, where `categoryIndex` is the position of the category in the group's current distinct-category list, ordered alphabetically (case-insensitive), recomputed fresh by the handler from `ShoppingItemRepository.GetDistinctCategoriesAsync(groupId)` — the same approach `list_next`/`list_prev` already use for `pageNumber`. If the category set has changed between render and tap (rare — items were re-tagged mid-session), the handler falls back to the "All" view rather than erroring. An "All" button (`list_filter:<groupChatId>:-1:1`) clears the filter.

### 5. Filtered view reuses existing pagination and rendering, scoped by category
`ShoppingListService.GetPagedItemsAsync` and `BuildListAsync` gain an optional `category` parameter (`null` = current unfiltered behavior, unchanged). When set, `ShoppingItemRepository.GetAllAsync` results are filtered to matching `Category` (case-insensitive exact match) before paging. No grouping/headers are introduced in either view — the "all" view stays exactly as today, and the filtered view is naturally single-category, so no headers are needed. This avoids the awkward case of a category being split across pages under grouped headers.

### 6. Filter button row only appears when categories exist
`BuildListAsync` computes distinct categories for the group; if none exist, no filter row is added (zero visual change for groups not using the feature). If categories exist, a row of up to 5 category buttons (alphabetical) is appended below the item rows, plus an "Все" button when a filter is currently active (to return to the unfiltered view). Category button labels are truncated to 20 chars + "…" if longer, matching the existing 30-char truncation precedent in `shop-suggestions` (shorter here since multiple buttons share one row).

## Risks / Trade-offs

- **[Risk]** Every `/buy` add (and every edit) now produces one extra follow-up message, adding friction for groups that don't want categories → **Mitigation**: the skip button (`"Без категории"`) resolves the prompt in one tap, matching the existing accepted friction of the price-capture flow after every "bought" action; no category is forced.
- **[Risk]** Different users spelling/casing the same category differently (e.g. "Химия" vs "химия" vs "Бытовая химия") fragments the taxonomy → **Mitigation**: suggestion buttons make picking an existing spelling the path of least resistance; exact free-text matching is a known v1 limitation, not solved by fuzzy matching, consistent with `Non-Goals`.
- **[Risk]** More than 5 distinct categories in a group makes the suggestion/filter list incomplete → **Mitigation**: filter row and suggestion buttons show the top 5 (filter: alphabetical; suggestions: by frequency), but any category (even outside the top 5) can still be applied via free-text reply; only the button surface is capped, matching the existing shop-suggestions cap of 5.
- **[Risk]** Category index drifting between render and callback tap if items are added/removed concurrently → **Mitigation**: described in Decision 4 — falls back to "All" view rather than showing wrong data or erroring.
- **[Trade-off]** No dedicated category-rename tooling — renaming a category means re-editing each item. Acceptable for v1 given typical list sizes; flagged as a candidate follow-up if usage shows otherwise.

## Migration Plan

1. Add `Category TEXT NULL` column to `ShoppingItems` (current tag, for `/list` filtering) and to `PurchaseHistory` (historical record, for suggestion ranking) via additive `ALTER TABLE` in `DatabaseInitializer`, each guarded by a `PRAGMA table_info` check (existing pattern).
2. Deploy; existing rows in both tables get `Category = NULL`. `/list` renders identically to today (no filter row, since no categories exist yet) and the category-capture prompt shows only the skip button (no suggestions) until the group's first few categorized purchases build up history.
3. No backfill needed — categories populate organically as users respond to the prompt on new adds/edits, and history accumulates as those items are eventually bought.
4. **Rollback**: revert the code deploy. Both columns are additive and nullable, so older code ignores them; no data loss.

## Open Questions

- Should the filter button row cap change (currently 5, matching `shop-suggestions`) if user feedback shows people want more categories visible at once? Deferred — revisit after real usage.
