## 1. SQLite Foundation

- [x] 1.1 Add `Microsoft.Data.Sqlite` NuGet package to `ProductTrackerBot.csproj`
- [x] 1.2 Create `Database/DatabaseInitializer.cs` — `IHostedService` that runs `CREATE TABLE IF NOT EXISTS` for `Groups` and `ShoppingItems` on startup
- [x] 1.3 Register `DatabaseInitializer` and SQLite connection string in `Program.cs`

## 2. Dispatcher & Interfaces

- [x] 2.1 Define `ICommandHandler` interface (`string Command`, `Task HandleAsync(Message, CancellationToken)`)
- [x] 2.2 Define `ICallbackHandler` interface (`string CallbackPrefix`, `Task HandleAsync(CallbackQuery, CancellationToken)`)
- [x] 2.3 Create `UpdateDispatcher : IUpdateHandler` that routes messages to `ICommandHandler` by command and callback queries to `ICallbackHandler` by prefix
- [x] 2.4 Replace `UpdateHandler` registration with `UpdateDispatcher` in `Program.cs`

## 3. Dialog State

- [x] 3.1 Create `PendingDialogService<TState>` — singleton `ConcurrentDictionary<(long chatId, long userId), TState>`

## 4. Group Repository

- [x] 4.1 Create `GroupRepository` with `GetOrCreateAsync(long chatId)` — upserts `Groups` row and returns the `Group` record

## 5. Shopping List Repository

- [x] 5.1 Create `ShoppingItemRepository` with:
  - `AddAsync(int groupId, string name, string? quantity, string addedByName)`
  - `GetAllAsync(int groupId)` → `IReadOnlyList<ShoppingItem>`
  - `DeleteAsync(int itemId)`
- [x] 5.2 Create `ShoppingListService` that composes group + item repositories and builds the formatted list message text and inline keyboard

## 6. /buy Command

- [x] 6.1 Create `BuyCommandHandler : ICommandHandler` — handles `/buy`, validates group chat, initiates 2-step dialog via `PendingDialogService`
- [x] 6.2 Create `BuyStepHandler` (message handler registered in `UpdateDispatcher`) that processes dialog step 1 (name) and step 2 (quantity), saves item, sends confirmation
- [x] 6.3 Register `BuyCommandHandler` in `Program.cs`

## 7. /list Command

- [x] 7.1 Create `ListCommandHandler : ICommandHandler` — calls `ShoppingListService`, posts or edits list message, handles 400-error repost fallback, saves `ListMessageId` to group

## 8. Inline Button Handlers

- [x] 8.1 Create `ShopDoneCallbackHandler : ICallbackHandler` (`CallbackPrefix = "shop:done:"`) — deletes item, updates list message, sends bought confirmation
- [x] 8.2 Create `ShopRemoveCallbackHandler : ICallbackHandler` (`CallbackPrefix = "shop:remove:"`) — deletes item, updates list message
- [x] 8.3 Register both callback handlers in `Program.cs`

## 9. Unit Tests

- [x] 9.1 Create `ProductTrackerBot.Tests` xUnit project in the solution
- [x] 9.2 Write unit tests for `PendingDialogService`: set state, get state, clear state, concurrent access
- [x] 9.3 Write unit tests for `ShoppingListService`: empty list message, single item, multiple items, message text format
- [x] 9.4 Write unit tests for `UpdateDispatcher`: routes known command, routes known callback prefix, logs and ignores unknown command, logs and ignores unknown callback

## 10. Integration Smoke Test

- [x] 10.1 Write an integration test using `Microsoft.Data.Sqlite` in-memory mode for `ShoppingItemRepository`: add item, get all, delete item
- [x] 10.2 Write an integration test for `GroupRepository`: first call creates row, second call returns same row
