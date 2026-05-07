## 1. ShopDoneCallbackHandler

- [x] 1.1 Create `Handlers/ShopDoneCallbackHandler.cs` implementing `ICallbackHandler` with `CallbackPrefix = "shop:done:"`
- [x] 1.2 Parse `itemId` from callback data using `string.Split(':')[2]` and `int.Parse`
- [x] 1.3 Answer the callback query first (`AnswerCallbackQueryAsync`) to dismiss the Telegram spinner
- [x] 1.4 Call `ShoppingItemRepository.DeleteAsync(itemId)` — treat 0-rows-affected as silent no-op
- [x] 1.5 Call `ShoppingListService` to refresh the list message in place (with 400-error repost fallback)
- [x] 1.6 Send group confirmation message `"<FirstName>: <Name> <Qty> — отмечено ✓"` after successful refresh

## 2. ShopRemoveCallbackHandler

- [x] 2.1 Create `Handlers/ShopRemoveCallbackHandler.cs` implementing `ICallbackHandler` with `CallbackPrefix = "shop:remove:"`
- [x] 2.2 Parse `itemId` from callback data using `string.Split(':')[2]` and `int.Parse`
- [x] 2.3 Answer the callback query (`AnswerCallbackQueryAsync`)
- [x] 2.4 Call `ShoppingItemRepository.DeleteAsync(itemId)` — treat 0-rows-affected as silent no-op
- [x] 2.5 Call `ShoppingListService` to refresh the list message in place (with 400-error repost fallback)

## 3. Registration

- [x] 3.1 Register `ShopDoneCallbackHandler` as `ICallbackHandler` in `Program.cs`
- [x] 3.2 Register `ShopRemoveCallbackHandler` as `ICallbackHandler` in `Program.cs`

## 4. Unit Tests

- [x] 4.1 Write unit tests for `ShopDoneCallbackHandler`: item deleted and list refreshed, stale callback (0 rows) is a no-op, confirmation message sent after deletion
- [x] 4.2 Write unit tests for `ShopRemoveCallbackHandler`: item deleted and list refreshed, stale callback (0 rows) is a no-op, no confirmation message sent
