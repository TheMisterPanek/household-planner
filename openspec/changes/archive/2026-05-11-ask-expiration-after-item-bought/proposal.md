## Why

When a user marks a shopping list item as "bought" via the `/list` interface, the bot already asks for store and price. However, it misses a natural moment to capture the expiry date of the purchased item. Shopping list items are *planned* — they don't have a meaningful expiry yet. The right moment to record expiry is right after purchase, because that's when the user holds the item and can read the date off the packaging.

Without this, expiry tracking is only available through the `/buy` command flow, which asks for expiry *before* the item is purchased (while it's still planned). That is the wrong moment.

## What Changes

- Add step 3 (expiry date) to the price-capture dialog that follows marking an item as bought
- Persist `ExpDate` on `PurchaseRecord` (new nullable column on `PurchaseHistory` table)
- Extend `ExpiryNotificationService` to also surface expiring bought items from `PurchaseHistory`
- Add "Skip" callback for the new expiry step consistent with existing price-capture skip pattern
- Include expiry date in the purchase confirmation message when provided

## Capabilities

### Modified Capabilities
- `price-capture-dialog`: Extended from 2 steps (store, price) to 3 steps (store, price, expiry). Existing skip flows updated to chain correctly.
- `expiry-notifications`: Now reads from both `ShoppingItems` (planned) and `PurchaseHistory` (bought) to build expiry summaries.
- `purchase-history`: `PurchaseRecord` gains an optional `ExpDate` field, with additive schema migration.

## Impact

- **Code**: `PriceCaptureDialogState`, `PriceCaptureStepHandler`, `PriceSkipCallbackHandler`, `PurchaseRecord`, `PurchaseHistoryRepository`, `ExpiryNotificationService`
- **Schema**: `PurchaseHistory` table gets new nullable `exp_date TEXT` column (ISO-8601)
- **APIs**: New `price:skip_expiry` callback for the expiry step; no new commands
- **Compatibility**: Existing `price:skip_store` and `price:skip_price` callbacks continue to work; skip_price now transitions to the expiry step rather than finishing the dialog

## Rollback Plan

Revert `PriceCaptureDialogState` step count and `PriceSkipCallbackHandler` to 2-step flow. The `exp_date` column on `PurchaseHistory` can stay (nullable, ignored). Remove expiry query from `ExpiryNotificationService`.

## Affected Teams

Single-user bot; impacts personal household tracking workflow.

## Cross-Cutting Dependencies

- **Persistence**: New nullable `exp_date TEXT` column on `PurchaseHistory` via `PRAGMA table_info` + `ALTER TABLE` migration pattern; no migration framework.
- **Localization**: New keys for the expiry prompt and skip button in the price-capture flow; confirmation message extended for when expiry is provided.
- **Error Handling**: Invalid date input replies with a localized error and stays on step 3; no exceptions thrown at handler level.
- **History Audit**: `ItemBought` payload already recorded before dialog starts; no additional audit record needed for expiry capture.
- **Callback data**: `price:skip_expiry` is 17 bytes — well within the 64-byte limit.
