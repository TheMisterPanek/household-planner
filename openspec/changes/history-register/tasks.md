## 1. Database Schema

- [ ] 1.1 Add `BotActionHistory` DDL to `DatabaseInitializer.StartAsync` using `CREATE TABLE IF NOT EXISTS` with columns: `Id INTEGER PRIMARY KEY AUTOINCREMENT`, `ChatId INTEGER NOT NULL`, `UserId INTEGER NOT NULL`, `UserName TEXT NOT NULL`, `ActionType TEXT NOT NULL`, `Payload TEXT NOT NULL`, `RecordedAt TEXT NOT NULL DEFAULT (datetime('now'))`

## 2. Models and Enums

- [ ] 2.1 Create `Models/BotActionType.cs` defining `enum BotActionType { ItemAdded, ItemBought, ItemRemoved, ListViewed, HistoryViewed }`
- [ ] 2.2 Create `Models/BotActionEntry.cs` as a record with properties: `Id`, `ChatId`, `UserId`, `UserName`, `ActionType` (BotActionType), `Payload`, `RecordedAt` (DateTime)
- [ ] 2.3 Create `Models/BotActionPayload.cs` (or similar) with payload record types (e.g., `ItemPayload { Name, Quantity? }`) and a `[JsonSerializable]`-annotated `BotActionPayloadContext : JsonSerializerContext` covering all payload types

## 3. Repository

- [ ] 3.1 Create `Repositories/IHistoryRepository.cs` declaring `Task RecordAsync(long chatId, long userId, string userName, BotActionType actionType, string payloadJson, CancellationToken ct)` and `Task<IReadOnlyList<BotActionEntry>> GetRecentAsync(long chatId, int limit, CancellationToken ct)`
- [ ] 3.2 Create `Repositories/HistoryRepository.cs` implementing `IHistoryRepository` using raw `SqliteConnection` (matching existing repository pattern), with source-generated JSON serialization for payloads
- [ ] 3.3 Register `IHistoryRepository` → `HistoryRepository` as a singleton in `Program.cs`

## 4. Unit Tests — Repository

- [ ] 4.1 Write xUnit tests for `HistoryRepository.RecordAsync`: verify row is inserted with correct `ChatId`, `UserId`, `UserName`, `ActionType`, and `Payload` (use an in-memory or temp SQLite file)
- [ ] 4.2 Write xUnit tests for `HistoryRepository.GetRecentAsync`: verify descending order, limit enforcement, and empty-list result for unknown chat

## 5. /history Command Handler

- [ ] 5.1 Create `Handlers/HistoryCommandHandler.cs` implementing `ICommandHandler` for the `/history` command; inject `IHistoryRepository`
- [ ] 5.2 Implement group-only guard: reply "Эта команда работает только в групповом чате." if `ChatId == UserId` (private chat)
- [ ] 5.3 Implement `GetRecentAsync(chatId, limit: 10)` call and format each entry as `"HH:mm dd.MM — UserName: ActionType"`; reply "История пуста." when list is empty
- [ ] 5.4 Register `HistoryCommandHandler` in `UpdateDispatcher`

## 6. Unit Tests — /history Handler

- [ ] 6.1 Write xUnit tests for `HistoryCommandHandler`: empty history returns "История пуста.", non-empty history returns formatted entries, private chat returns group-only message

## 7. Inject History Recording into Existing Handlers

- [ ] 7.1 Add `IHistoryRepository` constructor parameter to `BuyCommandHandler`; after a successful inline item save, call `RecordAsync` with `ItemAdded` wrapped in try/catch (log Warning on failure)
- [ ] 7.2 Add `IHistoryRepository` constructor parameter to `BuyStepHandler`; after a successful item save (quantity step), call `RecordAsync` with `ItemAdded` wrapped in try/catch
- [ ] 7.3 Add `IHistoryRepository` constructor parameter to `BuySkipCallbackHandler`; after a successful item save (skip-quantity), call `RecordAsync` with `ItemAdded` wrapped in try/catch
- [ ] 7.4 Add `IHistoryRepository` constructor parameter to `ShopDoneCallbackHandler`; after successful item deletion, call `RecordAsync` with `ItemBought` wrapped in try/catch
- [ ] 7.5 Add `IHistoryRepository` constructor parameter to `ShopRemoveCallbackHandler`; after successful item deletion, call `RecordAsync` with `ItemRemoved` wrapped in try/catch
- [ ] 7.6 Add `IHistoryRepository` constructor parameter to `ListCommandHandler`; after successfully posting or editing the list message, call `RecordAsync` with `ListViewed` wrapped in try/catch

## 8. Unit Tests — Modified Handlers

- [ ] 8.1 Write xUnit tests for `BuyCommandHandler` (inline path): verify `IHistoryRepository.RecordAsync` is called with `ItemAdded` after save; verify bot reply still sent when `RecordAsync` throws
- [ ] 8.2 Write xUnit tests for `BuyStepHandler` and `BuySkipCallbackHandler`: verify `RecordAsync` called with `ItemAdded` and failure is swallowed
- [ ] 8.3 Write xUnit tests for `ShopDoneCallbackHandler`: verify `RecordAsync` called with `ItemBought` and failure is swallowed
- [ ] 8.4 Write xUnit tests for `ShopRemoveCallbackHandler`: verify `RecordAsync` called with `ItemRemoved` and failure is swallowed
- [ ] 8.5 Write xUnit tests for `ListCommandHandler`: verify `RecordAsync` called with `ListViewed` and failure is swallowed

## 9. AOT and Build Verification

- [ ] 9.1 Confirm `BotActionPayloadContext` lists all payload types in `[JsonSerializable]` attributes and no runtime reflection is used
- [ ] 9.2 Run `dotnet publish` with AOT enabled (or trimmed build) and confirm no IL-link warnings related to the new code
- [ ] 9.3 Run full test suite and confirm all existing tests still pass
