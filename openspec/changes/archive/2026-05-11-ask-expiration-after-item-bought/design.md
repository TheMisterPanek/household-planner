## Context

When a user taps the "✓" done button on a shopping list item, `ShopDoneCallbackHandler` deletes the item and starts the price-capture dialog (2 steps: store name → price). After this flow completes, the expiry date of the purchased item is never recorded — the item is gone from `ShoppingItems`, and `PurchaseRecord` has no `ExpDate` field.

This change extends the price-capture dialog to 3 steps, adds expiry persistence on `PurchaseRecord`, and wires the `ExpiryNotificationService` to surface bought-item expirations.

## Goals / Non-Goals

**Goals:**
- Ask for expiry date after every successful "mark as bought" flow (step 3 of price-capture dialog)
- Persist expiry on `PurchaseHistory` with additive schema migration
- Surface bought-item expiries from `ExpiryNotificationService` alongside planned-item expiries
- Keep skip path fully working: skip store, skip price, and skip expiry all terminate the dialog gracefully

**Non-Goals:**
- Separate home-inventory table (out of scope; `PurchaseHistory` row as expiry carrier is sufficient)
- Editing expiry after dialog completion
- Expiry for items removed with the "remove" button (`shop:remove:` — not a purchase)

## Decisions

### 1. Extend `PriceCaptureDialogState` to 3 Steps

**Decision**: Add `Step = 3` for expiry and an `ExpDate` property on `PriceCaptureDialogState`.

**Rationale**: Follows the existing pattern (`BuyDialogState` uses the same step model). No new state class; no new dialog service registration. New step 3 runs after step 2 (price is entered or skipped).

**Current flow:**
```
shop:done → Step 1 (store) → Step 2 (price) → finish
```
**New flow:**
```
shop:done → Step 1 (store) → Step 2 (price) → Step 3 (expiry) → finish
```

### 2. `PriceSkipCallbackHandler` Chains Step 2 → Step 3

**Decision**: When the user skips price (step 2), instead of finishing, advance to step 3 and prompt for expiry. The existing `price:skip_expiry` callback terminates the dialog.

**Impact**: `PriceSkipCallbackHandler` gains the `price:skip_expiry` sub-case. `FinishDialogAsync` moves out of `PriceSkipCallbackHandler` into shared logic (or is called from the step 3 handler).

**Callback data sizes:**
- `price:skip_store` — 16 bytes ✓
- `price:skip_price` — 16 bytes ✓
- `price:skip_expiry` — 17 bytes ✓ (all within 64-byte Telegram limit)

### 3. Expiry Stored as Nullable `exp_date TEXT` on `PurchaseHistory`

**Decision**: Add column `exp_date TEXT NULL` to `PurchaseHistory` via `PRAGMA table_info` + `ALTER TABLE` migration in `DatabaseInitializer.StartAsync`.

**Rationale**: Keeps expiry data alongside the purchase record. No need for a separate table. `DateOnly` serialized as `yyyy-MM-dd` ISO-8601 text, consistent with `ShoppingItems.exp_date` convention.

### 4. `ExpiryNotificationService` Queries Both Tables

**Decision**: Add `PurchaseHistoryRepository.GetItemsWithExpiryAsync(groupId)` method that returns bought items with non-null `exp_date`. `ExpiryNotificationService` merges both result sets (planned and bought) before bucketing by urgency.

**Rationale**: Users care about expiry regardless of whether the item was tracked as planned or bought. A single merged summary prevents confusion about which items are being tracked.

### 5. Date Parsing: Reuse Existing `/buy` Logic

**Decision**: Extract date parsing from `BuyStepHandler.ParseExpiryInput` into a shared static helper (or duplicate it in the step handler — small duplication is acceptable). Accept the same formats: number of days, `N day(s)`, `N week(s)`, `N month(s)` in English and Russian.

**Rationale**: Users learn one format. Consistency reduces support burden.

## Flow Diagram

```
[User taps ✓ on list item]
        │
        ▼
ShopDoneCallbackHandler
  → deletes ShoppingItem
  → sends confirmation
  → sets PriceCaptureDialogState {Step=1}
  → sends "Where did you buy X?" with shop suggestions
        │
   ┌────┴────────────────────────────────────┐
   │ Text (store name)                        │ price:skip_store callback
   ▼                                          ▼
Step 1 complete                     Step 1 skipped
  StoreName=<input>                   StoreName=null
  Step=2                              Step=2
        │
        ▼
"💰 Price for X?" + [Skip]
        │
   ┌────┴────────────────────────────────────┐
   │ Text (price)                             │ price:skip_price callback
   ▼                                          ▼
Step 2 complete                     Step 2 skipped
  Price=<input>                       Price=null
  Step=3                              Step=3
        │
        ▼
"📅 Expiry date for X? (e.g. 30, 7 days)" + [Skip]
        │
   ┌────┴────────────────────────────────────┐
   │ Text (date)                              │ price:skip_expiry callback
   ▼                                          ▼
Step 3 complete                     Step 3 skipped
  ExpDate=<parsed>                    ExpDate=null
        │                                     │
        └──────────────┬──────────────────────┘
                       ▼
              FinishDialogAsync
                → PurchaseHistoryRepository.AddAsync(record with ExpDate)
                → PriceLogRepository.AddAsync (if price set)
                → dialogService.ClearState
                → "✓ X recorded [at Store] [for Price] [expires DD.MM.YYYY]"
```

## Schema Migration

In `DatabaseInitializer.StartAsync`:

```csharp
// Add exp_date column to PurchaseHistory if not present
var columns = new List<string>();
await using (var infoCmd = connection.CreateCommand()) {
    infoCmd.CommandText = "PRAGMA table_info(PurchaseHistory)";
    await using var rdr = await infoCmd.ExecuteReaderAsync();
    while (await rdr.ReadAsync()) columns.Add(rdr.GetString(1));
}
if (!columns.Contains("exp_date")) {
    await using var alterCmd = connection.CreateCommand();
    alterCmd.CommandText = "ALTER TABLE PurchaseHistory ADD COLUMN exp_date TEXT NULL";
    await alterCmd.ExecuteNonQueryAsync();
}
```

## Files Changed

| File | Change |
|------|--------|
| `DatabaseInitializer.cs` | Add `exp_date` migration for `PurchaseHistory` |
| `Models/PriceCaptureDialogState.cs` | Add `ExpDate` property |
| `Models/PurchaseRecord.cs` | Add `ExpDate` property |
| `Repositories/PurchaseHistoryRepository.cs` | Include `exp_date` in INSERT; add `GetItemsWithExpiryAsync` |
| `Handlers/PriceCaptureStepHandler.cs` | Add step 3 handling (text input → parse date → FinishDialogAsync) |
| `Handlers/PriceSkipCallbackHandler.cs` | Add `price:skip_price` → step 3 transition; add `price:skip_expiry` → finish |
| `Services/ExpiryNotificationService.cs` | Merge bought-item expiries from PurchaseHistory |
| `Localization/Strings.*.json` | New keys: `shop.expiry-prompt`, `shop.skip-expiry`, `shop.done-with-expiry` |

## AOT Compatibility

- No new reflection or dynamic code generation
- `PurchaseRecord` is a plain class, already serialized via source-gen context if needed
- Date parsing uses static string operations only
