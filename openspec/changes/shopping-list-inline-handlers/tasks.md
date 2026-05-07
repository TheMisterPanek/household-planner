## 1. ShopDoneCallbackHandler

- [ ] 1.1 Create `Handlers/ShopDoneCallbackHandler.cs` implementing `ICallbackHandler` with `CallbackPrefix = "shop:done:"`
- [ ] 1.2 Parse `itemId` from callback data using `string.Split(':')[2]` and `int.Parse`
- [ ] 1.3 Answer the callback query first (`AnswerCallbackQueryAsync`) to dismiss the Telegram spinner
- [ ] 1.4 Call `ShoppingItemRepository.DeleteAsync(itemId)` — treat 0-rows-affected as silent no-op
- [ ] 1.5 Call `ShoppingListService` to refresh the list message in place (with 400-error repost fallback)
- [ ] 1.6 Send group confirmation message `"<FirstName>: <Name> <Qty> — отмечено ✓"` after successful refresh

## 2. ShopRemoveCallbackHandler

- [ ] 2.1 Create `Handlers/ShopRemoveCallbackHandler.cs` implementing `ICallbackHandler` with `CallbackPrefix = "shop:remove:"`
- [ ] 2.2 Parse `itemId` from callback data using `string.Split(':')[2]` and `int.Parse`
- [ ] 2.3 Answer the callback query (`AnswerCallbackQueryAsync`)
- [ ] 2.4 Call `ShoppingItemRepository.DeleteAsync(itemId)` — treat 0-rows-affected as silent no-op
- [ ] 2.5 Call `ShoppingListService` to refresh the list message in place (with 400-error repost fallback)

## 3. Registration

- [ ] 3.1 Register `ShopDoneCallbackHandler` as `ICallbackHandler` in `Program.cs`
- [ ] 3.2 Register `ShopRemoveCallbackHandler` as `ICallbackHandler` in `Program.cs`

## 4. Unit Tests

- [ ] 4.1 Write unit tests for `ShopDoneCallbackHandler`: item deleted and list refreshed, stale callback (0 rows) is a no-op, confirmation message sent after deletion
- [ ] 4.2 Write unit tests for `ShopRemoveCallbackHandler`: item deleted and list refreshed, stale callback (0 rows) is a no-op, no confirmation message sent
