## Why

When a group member marks a shopping list item as bought, there is currently no way to record what was paid or where. Over time this means there is no price history to compare across shopping trips. Adding optional price capture at check-off time and a `/search` command turns the bot into a lightweight price tracker without disrupting the existing buy-flow.

## What Changes

- When a user taps the ✓ (done) button on a list item, the bot optionally prompts for store name and price before removing the item. Both fields are skippable.
- A new `PurchaseHistory` table persists each completed purchase with: item name, quantity, store name (optional), price (optional), date, and who bought it.
- A new `/search <query>` command searches purchase history by item name and returns matched entries grouped by item name, showing the latest price, price trend (`+N%` / `-N%`), and store.
- The existing instant-remove (✗ Убрать) button is unchanged — it deletes without recording history.

## Capabilities

### New Capabilities

- `purchase-history`: Stores completed purchase records per group (item name, quantity, store, price, date, buyer). Provides query access for the search command.
- `item-search`: `/search <query>` command that looks up purchase history by partial item name match and returns price dynamics compared to the previous purchase of the same item.

### Modified Capabilities

- `shopping-list`: The "mark as bought" (✓) flow gains an optional price-capture step before removing the item from the active list. The ✗ remove path is unchanged.

## Impact

- **Database**: New `PurchaseHistory` table (non-destructive addition). Existing `ShoppingItems` table unchanged.
- **Handlers**: `ShopDoneCallbackHandler` gains a dialog step; new `SearchCommandHandler` added.
- **Models**: New `PurchaseRecord` model.
- **Repositories**: New `PurchaseHistoryRepository`.
- **New command**: `/search` registered in the dispatcher.
- **Rollback**: Drop the `PurchaseHistory` table, revert `ShopDoneCallbackHandler` to current behavior, unregister `/search`. No existing data affected.
- **Affected teams**: Bot feature development only.
- **AOT compatibility**: No reflection used; raw SQLite queries and simple arithmetic for delta calculation.
