## 1. Database & Models

- [x] 1.1 Add `CREATE TABLE IF NOT EXISTS PurchaseHistory` to `DatabaseInitializer.StartAsync` with columns: `Id`, `GroupId`, `ItemName`, `Quantity` (nullable), `StoreName` (nullable), `Price REAL` (nullable), `PurchasedAt TEXT NOT NULL`, `BoughtByName TEXT NOT NULL`
- [x] 1.2 Create `PurchaseRecord` model record with all table columns as properties (`Price` as `decimal?`, `PurchasedAt` as `DateTime`)
- [x] 1.3 Create `PriceCaptureDialogState` class with `Step` (int), `ItemName`, `Quantity` (nullable), `BoughtByName`, and `StoreName` (nullable) properties

## 2. PurchaseHistoryRepository

- [x] 2.1 Create `PurchaseHistoryRepository` with `AddAsync(PurchaseRecord)` that inserts a row and returns the record with its new `Id`
- [x] 2.2 Add `SearchAsync(int groupId, string query)` that queries `PurchaseHistory` with `lower(ItemName) LIKE lower('%query%')` ordered by `PurchasedAt DESC` and returns `IReadOnlyList<PurchaseRecord>`
- [x] 2.3 Register `PurchaseHistoryRepository` in `Program.cs` DI

## 3. Unit Tests — Repository

- [x] 3.1 Test `PurchaseHistoryRepository.AddAsync` saves all fields including nullable ones set to null
- [x] 3.2 Test `SearchAsync` returns only records for the given `groupId`
- [x] 3.3 Test `SearchAsync` is case-insensitive and matches partial item names
- [x] 3.4 Test `SearchAsync` returns results in descending `PurchasedAt` order

## 4. Price-Capture Dialog

- [x] 4.1 Update `ShopDoneCallbackHandler`: before deleting the item, read its name/quantity from `ShoppingItemRepository`; after delete+list-update, set `PriceCaptureDialogState(step=1, ...)` via `PendingDialogService<PriceCaptureDialogState>` and send the "📍 Where did you buy {Name}?" prompt with a [Skip] button (callback data `price:skip_store`)
- [x] 4.2 Create `PriceCaptureStepHandler : IDialogMessageHandler` that handles step 1 (text → store name → advance to step 2, ask price with [Skip] `price:skip_price`) and step 2 (text → parse decimal price → save `PurchaseRecord` → clear dialog → send confirmation)
- [x] 4.3 Add price validation in step 2: if text is not parseable as decimal, reply with retry message and stay on step 2
- [x] 4.4 Create `PriceSkipCallbackHandler : ICallbackHandler` with `CallbackPrefix = "price:skip_"` that reads current step from dialog state; if step 1 skips store and advances to step 2; if step 2 skips price and saves record with `Price = null`, clears dialog, sends confirmation
- [x] 4.5 Register `PriceCaptureStepHandler` and `PriceSkipCallbackHandler` in `Program.cs` DI
- [x] 4.6 Register `PendingDialogService<PriceCaptureDialogState>` as singleton in `Program.cs` DI

## 5. Unit Tests — Price-Capture Dialog

- [ ] 5.1 Test `ShopDoneCallbackHandler` sets `PriceCaptureDialogState` with step=1 and sends the store-prompt message after deleting the item
- [ ] 5.2 Test `PriceCaptureStepHandler` step 1 saves store name to state and asks for price
- [ ] 5.3 Test `PriceCaptureStepHandler` step 2 with valid decimal saves `PurchaseRecord` and clears dialog
- [ ] 5.4 Test `PriceCaptureStepHandler` step 2 with non-numeric input replies with retry message and stays on step 2
- [ ] 5.5 Test `PriceSkipCallbackHandler` on step 1 skips store and advances to step 2
- [ ] 5.6 Test `PriceSkipCallbackHandler` on step 2 saves record with `Price = null` and clears dialog

## 6. /search Command

- [ ] 6.1 Create `SearchCommandHandler : ICommandHandler` for `/search`; if no argument, reply with usage guidance; otherwise call `PurchaseHistoryRepository.SearchAsync`
- [ ] 6.2 Implement result grouping in `SearchCommandHandler`: group returned records by `ItemName` (case-insensitive); for each group, format the latest purchase line with store (if set) and price (if set)
- [ ] 6.3 Implement price delta calculation: `round((latest - previous) / previous * 100, 1)`, shown as `+N.N%` or `-N.N%` only when both the latest and second-latest records in a group have non-null `Price`
- [ ] 6.4 Format the "no results" reply: "No purchase history found for '{query}'"
- [ ] 6.5 Register `SearchCommandHandler` in `Program.cs` DI

## 7. Unit Tests — /search

- [ ] 7.1 Test `SearchCommandHandler` with no argument replies with usage guidance
- [ ] 7.2 Test `SearchCommandHandler` with results formats each item group correctly
- [ ] 7.3 Test price delta shown as `+N.N%` when price increased
- [ ] 7.4 Test price delta shown as `-N.N%` when price decreased
- [ ] 7.5 Test no delta shown when only one purchase record exists for an item
- [ ] 7.6 Test no delta shown when latest purchase has no price
- [ ] 7.7 Test "no results" reply when `SearchAsync` returns an empty list
