## 1. Data Model

- [x] 1.1 Add `TopShops: List<string>?` field to `PriceCaptureDialogState` class to store top shops fetched at step 1

## 2. Repository Implementation

- [x] 2.1 Implement `PurchaseHistoryRepository.GetTopShopsAsync(int groupId, long userId, int limit)` that queries `PurchaseHistory` for the user's top shops by frequency (count DESC, PurchasedAt DESC), filtering out null `StoreName` values, and returns `IReadOnlyList<string>`
- [x] 2.2 Truncate shop names to 30 characters + "…" if they exceed the limit in the query or at retrieval

## 3. Unit Tests — Repository

- [x] 3.1 Test `GetTopShopsAsync` returns shops ordered by frequency (highest first)
- [x] 3.2 Test `GetTopShopsAsync` breaks frequency ties by most recent `PurchasedAt`
- [x] 3.3 Test `GetTopShopsAsync` excludes null `StoreName` values
- [x] 3.4 Test `GetTopShopsAsync` respects the `limit` parameter (e.g., limit=5 returns at most 5 shops)
- [x] 3.5 Test `GetTopShopsAsync` returns empty list when user has no purchases in the group
- [x] 3.6 Test `GetTopShopsAsync` is scoped to the given `groupId` and `userId` (different users see different shops)

## 4. Dialog Handler Updates

- [x] 4.1 Update `PriceCaptureStepHandler.HandleStep1Async` to call `PurchaseHistoryRepository.GetTopShopsAsync` and store result in `state.TopShops`
- [x] 4.2 Build inline keyboard buttons from `state.TopShops` (one button per shop, each with callback data `price:shop:<0-indexed-shop-number>`)
- [x] 4.3 Add [Skip] button to the keyboard
- [x] 4.4 Send message with shop buttons and [Skip] button below the "📍 Where did you buy {item}?" prompt
- [x] 4.5 Create `PriceShopSuggestionCallbackHandler : ICallbackHandler` with `CallbackPrefix = "price:shop:"` that handles shop button taps, sets `StoreName` from the tapped shop, advances to step 2, and asks for price
- [x] 4.6 Register `PriceShopSuggestionCallbackHandler` in `Program.cs` DI

## 5. Unit Tests — Dialog Handler

- [x] 5.1 Test `PriceCaptureStepHandler.HandleStep1Async` fetches top shops and stores them in state
- [x] 5.2 Test `HandleStep1Async` creates inline buttons for each top shop (with correct callback data format `price:shop:N`)
- [x] 5.3 Test `HandleStep1Async` includes [Skip] button alongside shop buttons
- [x] 5.4 Test `PriceShopSuggestionCallbackHandler` tapping a shop button sets `StoreName` and advances to step 2
- [x] 5.5 Test `PriceShopSuggestionCallbackHandler` tapping a shop button sends "💰 Price for {item}?" prompt with [Skip] button
- [x] 5.6 Test step 1 with zero top shops available (user is new) shows only [Skip] button and waits for text input
- [x] 5.7 Test step 1 with fewer than 5 top shops shows only those available (not padded to 5)

## 6. Integration

- [x] 6.1 Verify no breaking changes to existing `/buy`, `/list`, `/search`, or `/history` commands
- [x] 6.2 Test full price-capture flow: user marks item bought, sees shop suggestions, taps one, enters price, records purchase
- [x] 6.3 Test fallback: user ignores shop suggestions and types a custom shop name in step 1
- [x] 6.4 Test edge case: shop names longer than 30 characters are truncated with ellipsis in button labels
