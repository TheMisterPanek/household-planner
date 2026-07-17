## Why

Shopping lists mix unrelated items (groceries, household chemicals, pharmacy items, etc.) in one flat list. When a shopper is physically in a store, they want to jump straight to "household chemicals" or "for the washing machine" instead of scanning the whole list. There is currently no way to group or tag items, so users can't filter the list down to a single category while shopping.

## What Changes

- Add an optional free-text `Category` (tag/group) to shopping items, e.g. "Бытовая химия".
- After any `/buy` add completes (inline single, dialog, or bulk) and persists the item(s), the bot follows up by asking which category to add to — mirroring the existing "📍 Where did you buy?" follow-up that already runs after marking an item bought. The prompt shows the group's most-used categories as inline buttons (frequency-ranked) plus a "Без категории" (skip) button.
- A user creates a new category simply by replying to that prompt with free text instead of tapping a suggestion — the same mechanism already used for typing a custom shop name instead of tapping a suggested one.
- For bulk `/buy` (comma-separated items), the prompt is asked once for the whole batch and applies the chosen category to all items just added.
- The one-line item-edit flow also re-triggers the category prompt after saving name/quantity changes, so a category can be added, changed, or cleared later.
- Category suggestions are ranked per **group** (not per user, unlike shop suggestions) since a category taxonomy is a shared, group-wide concept — everyone adding "Бытовая химия" items should see and reuse the same tag. Suggestions are sourced from `PurchaseHistory` (permanent), not the live `ShoppingItems` table, so they don't disappear as items get bought and removed from the active list.
- Add category filter buttons to the `/list` message: tapping a category shows only items in that category (still paginated); a "Все" (All) button returns to the full list. Buttons only appear when at least one active item has a category.
- **BREAKING**: none — this is purely additive; existing `/buy`/`/list` scenarios without categories are unaffected except for the one new follow-up message after every add/edit.

### Modified Capabilities
- `shopping-list`: `/buy` add flows (inline, bulk, dialog) trigger a follow-up category prompt after persisting item(s); the one-line edit flow re-triggers it too; `/list` gains category filter buttons and a filtered single-category view.

### New Capabilities
- `product-tags`: category storage on `ShoppingItems` and `PurchaseHistory`, the category-capture follow-up dialog (suggestions + free-text-creates-new + skip), group-wide frequency ranking, and the filtered list view.

## Impact

- **Database**: `ShoppingItems` gains a nullable `Category` column (current tag, used for `/list` filtering); `PurchaseHistory` gains a nullable `Category` column (historical record, used for suggestion ranking). Both added via `ALTER TABLE ... ADD COLUMN` in `DatabaseInitializer`, following the existing additive-migration pattern (see `exp_date` migration).
- **Models**: `ShoppingItem` gains `Category`; `PurchaseRecord` gains `Category`; `PriceCaptureDialogState` gains `Category` (carried through to the purchase record when an item is later marked bought); new `CategoryCaptureDialogState` (mirrors `PriceCaptureDialogState`'s shape: item id(s), group id, suggested categories).
- **Repositories**: `ShoppingItemRepository` — `AddAsync`/`UpdateAsync` accept `category`; new method to fetch distinct categories for a group (from active items, for the `/list` filter). `PurchaseHistoryRepository` — new `GetTopCategoriesAsync(groupId, limit)`, group-scoped (mirrors the existing per-user `GetTopShopsAsync` shape but ranked group-wide, not per-user).
- **Services**: `ShoppingListService` — threads an optional category filter through `GetPagedItemsAsync`/`BuildListAsync`, builds the category filter button row.
- **Handlers**: `BuyConfirmCallbackHandler`, `BuySkipCallbackHandler`, `BuyCommandHandler.HandleBulkAddAsync`, and `ItemSaveCallbackHandler` each start the category-capture follow-up after persisting item(s); new `CategoryCaptureStepHandler` (`IDialogMessageHandler`) handles the free-text reply; new `CategorySuggestCallbackHandler` (prefix `category:suggest:`) and `CategorySkipCallbackHandler` (prefix `category:skip`) handle button taps; new `ListFilterCallbackHandler` (prefix `list_filter:`) renders the filtered `/list` view.
- **Localization**: new keys for the category prompt, suggestion/skip buttons, and confirmation text in `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`.
- **Rollback plan**: both new columns are additive and nullable, so rolling back the code deploy leaves existing data intact (older code simply ignores the columns). No destructive migration is introduced.
- **AOT compatibility**: no reflection-based JSON or DI patterns introduced; category values are plain string columns, not JSON payloads, so no new `JsonSerializerContext` entries are required.
- **Affected teams**: none beyond the bot maintainer(s); no external API or web UI changes (Web UI is currently disabled per recent commit history).
- **Cross-cutting decisions relied on**: SQLite raw ADO additive migration; ISO-8601 not applicable (no new dates); Telegram dispatch via existing `ICallbackHandler`/`ICommandHandler`/dialog trio (no new routing mechanism) — the category-capture dialog reuses the exact `PendingDialogService<TState>` + `IDialogMessageHandler` shape already used by `PriceCaptureDialogState`; callback data stays within Telegram's 64-byte limit because suggestion/skip callbacks carry no embedded item data — they resolve the active `CategoryCaptureDialogState` from `(chatId, userId)` exactly as `price:shop:{index}`/`price:skip_store` do today; all new user-facing strings go through `ILocalizer`.
