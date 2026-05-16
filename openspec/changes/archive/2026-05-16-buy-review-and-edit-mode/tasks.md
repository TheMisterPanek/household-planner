## 1. Add Localization Keys

- [x] 1.1 Add all new keys to `Strings.en.json`:
  - `buy.review-add`, `buy.review-add-qty`, `buy.btn-confirm`, `buy.btn-edit`, `buy.btn-cancel`, `buy.cancelled`, `buy.edit-prompt`
  - `item.edit-prompt`, `item.review-save`, `item.review-save-qty`, `item.btn-save`, `item.btn-cancel`, `item.saved`, `item.saved-qty`, `item.edit-button`
- [x] 1.2 Add the same keys with Russian translations to `Strings.ru.json`
- [x] 1.3 Add the same keys with Polish translations to `Strings.pl.json`

## 2. BuyInputParser

- [x] 2.1 Create `ProductTrackerBot/Services/BuyInputParser.cs` — static class with:
  - `[GeneratedRegex(...)]` attribute for the quantity-pattern regex (AOT-safe)
  - `Parse(string rawText) → (string Name, string? Quantity)` method:
    - Strip leading "купить " prefix (case-insensitive)
    - Match trailing quantity pattern (number + optional unit from Russian/Latin unit list)
    - If no unit match, check if last token is a standalone integer (bare-number quantity)
    - Return trimmed name and normalized quantity (unit normalized to short form, e.g. "штуку" → "шт")
    - Return (fullText, null) if no quantity detected

## 3. PendingAddService and Models

- [x] 3.1 Create `ProductTrackerBot/Models/PendingAddItem.cs` — record with `ChatId`, `GroupId`, `Name`, `Quantity`, `AddedByName`
- [x] 3.2 Create `ProductTrackerBot/Services/PendingAddService.cs` — singleton with `Store`, `Get`, `Clear`; token = 8-char lowercase hex from 4 random bytes
- [x] 3.3 Register `PendingAddService` as `AddSingleton` in `Program.cs`

## 4. Modify BuyCommandHandler

- [x] 4.1 Replace `ParseArgs` with `BuyInputParser.Parse`
- [x] 4.2 After parse: store in `PendingAddService`; send review message with three buttons (`buy:confirm:{token}`, `buy:edit:{token}`, `buy:cancel:{token}`) using localization keys `buy.review-add` / `buy.review-add-qty`, `buy.btn-confirm`, `buy.btn-edit`, `buy.btn-cancel`
- [x] 4.3 Remove the old `ParseArgs` private method and the direct `AddAsync` + history-record path from the inline-args branch

## 5. Modify BuyDialogState and BuyStepHandler

- [x] 5.1 Add `bool IsOneLineMode` property to `BuyDialogState`
- [x] 5.2 In `BuyStepHandler.HandleStep1Async`: if `state.IsOneLineMode`, call `BuyInputParser.Parse` on the text, store pending item in `PendingAddService`, clear dialog state, send review message with [✓ Add] [✕ Cancel]; else keep existing 2-step behavior
- [x] 5.3 In `BuyStepHandler.HandleStep2Async`: replace `FinishDialogAsync` (which calls `AddAsync`) with storing pending item in `PendingAddService` and sending review message with [✓ Add] [✏️ Edit] [✕ Cancel]
- [x] 5.4 Remove `FinishDialogAsync` (it will be replaced by `BuyConfirmCallbackHandler`)

## 6. Add Buy Review Callback Handlers

- [x] 6.1 Create `ProductTrackerBot/Handlers/BuyConfirmCallbackHandler.cs` (`ICallbackHandler`, prefix `"buy:confirm:"`):
  - Parse token from callback data
  - `PendingAddService.Get(token)` → if null, answer callback with "Session expired" and return
  - `PendingAddService.Clear(token)`
  - `ShoppingItemRepository.AddAsync(...)`
  - Send localized confirm message (`buy.item-added` / `buy.item-added-quantity`)
  - `IHistoryRepository.RecordAsync(ItemAdded)` (try/catch)
  - `AnswerCallbackQuery`
- [x] 6.2 Create `ProductTrackerBot/Handlers/BuyEditCallbackHandler.cs` (`ICallbackHandler`, prefix `"buy:edit:"`):
  - Parse token → `PendingAddService.Get(token)` → if null, answer expired and return
  - `PendingAddService.Clear(token)`
  - Set `BuyDialogState { Step=1, IsOneLineMode=true, GroupId=..., AddedByName=... }` via `PendingDialogService<BuyDialogState>`
  - Send `buy.edit-prompt` message
  - `AnswerCallbackQuery`
- [x] 6.3 Create `ProductTrackerBot/Handlers/BuyCancelCallbackHandler.cs` (`ICallbackHandler`, prefix `"buy:cancel:"`):
  - Parse token → `PendingAddService.Clear(token)` (no-op if already gone)
  - Send `buy.cancelled` message
  - `AnswerCallbackQuery`
- [x] 6.4 Register all three handlers as `AddScoped<ICallbackHandler, ...>` in `Program.cs`

## 7. ShoppingItemRepository — UpdateAsync

- [x] 7.1 Add `UpdateAsync(int itemId, string name, string? quantity)` to `ShoppingItemRepository`:
  - `UPDATE ShoppingItems SET Name = @name, Quantity = @quantity WHERE Id = @id`
  - Virtual method (for mockability in tests)

## 8. Edit Existing Item — Models and Services

- [x] 8.1 Create `ProductTrackerBot/Models/EditItemDialogState.cs` — `{ int ItemId, int GroupId, string OriginalName, string? OriginalQuantity }`
- [x] 8.2 Create `ProductTrackerBot/Models/PendingEditItem.cs` — record with `ChatId`, `ItemId`, `GroupId`, `Name`, `Quantity`
- [x] 8.3 Create `ProductTrackerBot/Services/PendingEditService.cs` — same Store/Get/Clear pattern as `PendingAddService`
- [x] 8.4 Register `PendingDialogService<EditItemDialogState>` as `AddSingleton` and `PendingEditService` as `AddSingleton` in `Program.cs`

## 9. Add [✏️] Button to List View

- [x] 9.1 In `ShoppingListService.BuildListAsync`, add a third button per item row:
  - Label: `localizer.Get(chatId, "item.edit-button")` (= `"✏️"`)
  - Callback: `$"item:edit:{item.Id}"`
  - Row order: [✓ Done] [✏️] [Remove]

## 10. Item Edit Callback and Dialog Handlers

- [x] 10.1 Create `ProductTrackerBot/Handlers/ItemEditCallbackHandler.cs` (`ICallbackHandler`, prefix `"item:edit:"`):
  - Parse `itemId` from callback data
  - `ShoppingItemRepository.GetByIdAsync(itemId)` → if null, answer expired
  - Set `EditItemDialogState` via `PendingDialogService<EditItemDialogState>`
  - Send `item.edit-prompt` with item name substitution
  - `AnswerCallbackQuery`
- [x] 10.2 Create `ProductTrackerBot/Handlers/ItemEditStepHandler.cs` (`IDialogMessageHandler`):
  - `CanHandle`: `PendingDialogService<EditItemDialogState>.GetState(chatId, userId) is not null`
  - `HandleAsync`:
    - `BuyInputParser.Parse(message.Text)` → name, qty
    - `PendingDialogService<EditItemDialogState>.ClearState(...)`
    - `token = PendingEditService.Store(PendingEditItem { ... })`
    - Send review message (`item.review-save` / `item.review-save-qty`) with [✓ Save] [✕ Cancel] (`item:save:{token}`, `item:cancel-edit:{token}`)
- [x] 10.3 Create `ProductTrackerBot/Handlers/ItemSaveCallbackHandler.cs` (`ICallbackHandler`, prefix `"item:save:"`):
  - Parse token → `PendingEditService.Get(token)` → if null, answer expired
  - `PendingEditService.Clear(token)`
  - `ShoppingItemRepository.UpdateAsync(pending.ItemId, pending.Name, pending.Quantity)`
  - Refresh list message (reuse `ShoppingListService.BuildListAsync` + `EditMessageText` pattern from `ShopDoneCallbackHandler`)
  - Send `item.saved` / `item.saved-qty` confirmation
  - `IHistoryRepository.RecordAsync(ItemEdited)` (try/catch) — use `BotActionType.ItemEdited` if it exists, else `ItemAdded`
  - `AnswerCallbackQuery`
- [x] 10.4 Create `ProductTrackerBot/Handlers/ItemCancelEditCallbackHandler.cs` (`ICallbackHandler`, prefix `"item:cancel-edit:"`):
  - `PendingEditService.Clear(token)`
  - Send `buy.cancelled` (reuse key)
  - `AnswerCallbackQuery`
- [x] 10.5 Register all handlers in `Program.cs` as `AddScoped<ICallbackHandler, ...>` and `AddScoped<IDialogMessageHandler, ItemEditStepHandler>`

## 11. Unit Tests — BuyInputParser

- [x] 11.1 `"купить отривин 1 штуку"` → name=`"отривин"`, qty=`"1 шт"`
- [x] 11.2 `"молоко 2 л"` → name=`"молоко"`, qty=`"2 л"`
- [x] 11.3 `"хлеб"` (no qty) → name=`"хлеб"`, qty=`null`
- [x] 11.4 `"хлеб 2"` (bare number) → name=`"хлеб"`, qty=`"2"`
- [x] 11.5 `"зубная паста colgate max fresh"` (no trailing number) → full text as name, qty=`null`
- [x] 11.6 `"отривин 1 штуку"` (no "купить" prefix) → name=`"отривин"`, qty=`"1 шт"`
- [x] 11.7 `"молоко 0.5 л"` (decimal) → name=`"молоко"`, qty=`"0.5 л"`
- [x] 11.8 `"eggs 12 pcs"` (Latin units) → name=`"eggs"`, qty=`"12 pcs"`

## 12. Unit Tests — BuyCommandHandler

- [x] 12.1 Inline args → `PendingAddService.Store` called, review message sent with three buttons; `AddAsync` NOT called
- [x] 12.2 No inline args → dialog started (existing behavior unchanged)

## 13. Unit Tests — BuyStepHandler

- [x] 13.1 Step 1 normal mode → goes to step 2 (unchanged)
- [x] 13.2 Step 1 one-line mode → calls `BuyInputParser.Parse`, stores in `PendingAddService`, sends 2-button review; `AddAsync` NOT called
- [x] 13.3 Step 2 → stores in `PendingAddService`, sends 3-button review; `AddAsync` NOT called

## 14. Unit Tests — BuyConfirmCallbackHandler

- [x] 14.1 Valid token → `AddAsync` called, history recorded, confirm message sent
- [x] 14.2 Expired/missing token → callback answered with error, nothing else called

## 15. Unit Tests — BuyEditCallbackHandler

- [x] 15.1 Valid token → dialog state set with `IsOneLineMode=true`, edit prompt sent, session cleared
- [x] 15.2 Expired token → error response

## 16. Unit Tests — BuyCancelCallbackHandler

- [x] 16.1 Cancels session, sends cancelled message

## 17. Unit Tests — ShoppingListService

- [x] 17.1 Each item row has three buttons: Done, Edit (✏️), Remove
- [x] 17.2 Edit button callback data is `"item:edit:{id}"` for each item

## 18. Unit Tests — ShoppingItemRepository.UpdateAsync

- [x] 18.1 Updates name and quantity for a known item ID (in-memory SQLite)
- [x] 18.2 No-op (no exception) for unknown item ID

## 19. Unit Tests — ItemEditStepHandler

- [x] 19.1 CanHandle: returns true when `EditItemDialogState` is active, false otherwise
- [x] 19.2 HandleAsync: parses input, clears dialog, stores pending edit, sends review with 2 buttons

## 20. Unit Tests — ItemSaveCallbackHandler

- [x] 20.1 Valid token → `UpdateAsync` called, list refreshed, confirm sent, history recorded
- [x] 20.2 Expired token → answered with error

## 21. Manual Testing

- [x] 21.1 `/buy купить отривин 1 штуку` → review shows "Add: отривин ×1 шт?" → tap Add → item appears in `/list` correctly
- [x] 21.2 Same command → tap Edit → re-enter "отривин 2 шт" → review shows new values → tap Add → correct item added
- [x] 21.3 Same command → tap Cancel → no item added
- [x] 21.4 `/buy` (no args) → 2-step dialog → step 2 → review → confirm → item added
- [x] 21.5 `/buy хлеб` (no qty) → review shows "Add: хлеб?" → confirm → item with no quantity added
- [x] 21.6 `/list` → each item has ✏️ button → tap → edit prompt → type new text → review → save → list updated
- [x] 21.7 Edit cancel flow → item unchanged in list
- [x] 21.8 Existing `/buy` dialog (Skip quantity) → still adds immediately without review
- [x] 21.9 All existing commands (`/list`, `/meals`, `/history`, `/ai`) unaffected
