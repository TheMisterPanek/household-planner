## 1. Localization

- [x] 1.1 Add `shop.bulk-added` key to `Localization/Strings.en.json` with template: "{User} added {Count} items: {Items}"
- [x] 1.2 Add `shop.bulk-added` key to `Localization/Strings.ru.json` with appropriate Russian pluralization
- [x] 1.3 Add `shop.bulk-added` key to `Localization/Strings.pl.json` with appropriate Polish pluralization

## 2. Unit Tests

- [x] 2.1 Create `ProductTrackerBot.Tests/Services/ShoppingListServiceTests.cs` with unit tests for CSV parsing:
  - Test single item (no comma): `/buy Молоко` → one item, no quantity
  - Test single item with quantity: `/buy Молоко 2л` → one item with quantity
  - Test comma-separated items without quantities: `/buy Молоко, Яйца, Хлеб` → three items
  - Test comma-separated items with quantities: `/buy Молоко 2л, Яйца 6, Хлеб` → three items with respective quantities
  - Test whitespace trimming: `/buy Молоко  ,  Яйца  ,  Хлеб` → three items without quantity
  - Test item limit (max 20): `/buy item1, item2, ... item21` → only first 20 added
  - Test empty segments after split: `/buy Молоко,,Яйца` → handles gracefully

- [x] 2.2 Create `ProductTrackerBot.Tests/Handlers/BuyCommandHandlerTests.cs` with unit tests for handler:
  - Test handler routes single item to service as before
  - Test handler routes CSV items to service
  - Test handler calls history repository for each item
  - Test handler suppresses user response on item add failures (existing behavior preserved)
  - Test handler confirms bulk addition with correct localized message

## 3. Core Implementation

- [x] 3.1 Update `ShoppingListService.AddItemAsync()` to accept optional parameter for parsing CSV input (or create new overload `AddItemsAsync()` for bulk)

- [x] 3.2 Implement CSV parsing logic in `ShoppingListService`:
  - Split input by comma
  - Trim whitespace from each segment
  - Filter empty segments
  - Extract quantity (last token) from each item
  - Enforce 20-item limit
  - Call existing `AddItemAsync()` for each parsed item

- [x] 3.3 Update `BuyCommandHandler.Handle()` to:
  - Detect comma in inline argument
  - If comma present, invoke bulk add path
  - If no comma, use existing single-item path (backward compatibility)
  - Pass necessary context (chat ID, user ID, group) to service

- [x] 3.4 Update `BuyCommandHandler.Handle()` to generate correct confirmation message:
  - For single item: reuse existing "User добавил(а) Item Qty" message
  - For multiple items: use new `shop.bulk-added` localization key with item count and list
  - Truncate item list if too long (optional, but recommended for readability)

- [x] 3.5 Update `BuyCommandHandler.Handle()` to record history for each added item:
  - Call `IHistoryRepository.RecordAsync()` once per item (not bulk)
  - Wrap in try/catch per existing pattern
  - Log at Warning on failure
  - Do not suppress user confirmation on history write failure

## 4. Integration Tests

- [x] 4.1 Add to `ProductTrackerBot.Tests/Integration/BuyCommandHandlerIntegrationTests.cs`:
  - Test `/buy Молоко, Яйца, Хлеб` creates three items with no quantity
  - Test `/buy Молоко 2л, Яйца 6` creates two items with respective quantities
  - Test `/buy Молоко  ,  Яйца` trims whitespace and creates two items
  - Test `/buy item1, item2, ... item21` creates only first 20 items
  - Test bulk addition records history entry for each item
  - Test bulk confirmation message uses correct localization key
  - Test existing single-item flows still work (no regression)

- [x] 4.2 Verify integration tests run without errors: `make test`

## 5. Manual Smoke Test

- [ ] 5.1 Start bot: `make run` with valid `BOT_TOKEN` in `.env`

- [ ] 5.2 Test single-item `/buy` (backward compatibility):
  - Send `/buy Молоко 2л` in group
  - Verify bot confirms "Иван добавил(а) Молоко 2л"
  - Verify item appears in `/list`

- [ ] 5.3 Test comma-separated items without quantities:
  - Send `/buy Яйца, Хлеб, Масло` in group
  - Verify bot confirms "Иван добавил(а) 3 товара: Яйца, Хлеб, Масло"
  - Verify three items appear in `/list` with no quantity

- [ ] 5.4 Test comma-separated items with quantities:
  - Send `/buy Молоко 1л, Яйца 6, Хлеб` in group
  - Verify bot confirms "Иван добавил(а) 3 товара: Молоко 1л, Яйца 6, Хлеб"
  - Verify three items appear in `/list` with correct quantities

- [ ] 5.5 Test whitespace handling:
  - Send `/buy Молоко  ,  Яйца  ,  Хлеб` (extra spaces)
  - Verify bot confirms correctly without extra spaces in item names

- [ ] 5.6 Test item limit (20 items):
  - Send `/buy item1, item2, item3, ... item25` (25 items)
  - Verify only first 20 are added
  - Check bot log shows warning about limit

- [ ] 5.7 Test dialog flow still works (no regression):
  - Send `/buy` with no arguments
  - Verify bot asks "Что купить?"
  - Complete the dialog and verify item is added

- [ ] 5.8 Test private chat rejection (no regression):
  - Send `/buy Молоко` in private chat with bot
  - Verify bot replies "Эта команда работает только в групповом чате."

- [ ] 5.9 Check `/history` or audit log to verify bulk items recorded individually in history
