## 1. Database Schema

- [x] 1.1 Add `CREATE TABLE IF NOT EXISTS PriceLog` in `DatabaseInitializer.InitializeAsync` with columns: `Id` INTEGER PK, `GroupId` INTEGER NOT NULL, `ItemName` TEXT NOT NULL, `Price` REAL NOT NULL, `StoreName` TEXT, `LoggedAt` TEXT NOT NULL

## 2. Models

- [x] 2.1 Create `Models/PriceLogEntry.cs` record with `Id`, `GroupId`, `ItemName`, `Price` (decimal), `StoreName` (nullable string), `LoggedAt` (DateTime)
- [x] 2.2 Create `Models/PriceStats.cs` record with `Min`, `Avg`, `Max` (all decimal), `Count` (int), and `StoreBreakdown` (`IReadOnlyList<StoreStats>`)
- [x] 2.3 Create `Models/StoreStats.cs` record with `StoreName` (nullable string), `Min`, `Avg`, `Max` (all decimal), `Count` (int)

## 3. PriceLog Repository

- [x] 3.1 Create `Repositories/PriceLogRepository.cs` with `AddAsync(groupId, itemName, price, storeName, loggedAt)` → `PriceLogEntry`
- [x] 3.2 Implement `GetStatsAsync(groupId, itemName)` → `PriceStats?`: overall min/avg/max/count using `LOWER(ItemName) LIKE LOWER('%'||@query||'%')` exact match (`= LOWER(@name)` for stats), returns `null` when no rows match
- [x] 3.3 Implement store breakdown in `GetStatsAsync`: per-store min/avg/max grouped by `StoreName`, limited to top 10 by count; null store names grouped under a single null-key entry

## 4. Case-Insensitive Fix — PurchaseHistoryRepository

- [x] 4.1 Update `SearchAsync` query to use `LOWER(ItemName) LIKE '%' || LOWER(@query) || '%'` instead of raw `LIKE` to fix non-ASCII (Cyrillic, Polish) case-insensitive matching

## 5. Price-Capture Integration

- [x] 5.1 Inject `PriceLogRepository` into `PriceCaptureStepHandler` (or whichever handler completes the price-capture dialog)
- [x] 5.2 After saving a `PurchaseRecord` with non-null price, call `PriceLogRepository.AddAsync` with item name, price, store name, and `DateTime.UtcNow`; wrap in try/catch, log Warning on failure

## 6. /prices Command Handler

- [x] 6.1 Create `Handlers/PricesCommandHandler.cs` implementing `ICommandHandler` with `Command = "/prices"`
- [x] 6.2 Reject private chats with localized "group chat only" message
- [x] 6.3 Reply with `prices.usage` key when no argument is provided
- [x] 6.4 Call `PriceLogRepository.GetStatsAsync` with trimmed argument; reply with `prices.not-found` key when result is null
- [x] 6.5 Format and send stats reply: overall min/avg/max/count + per-store breakdown (prices to 2 decimal places); use localized keys for all labels

## 7. Localization Keys

- [x] 7.1 Add keys to `Strings.en.json`: `prices.usage`, `prices.not-found`, `prices.header`, `prices.stats-line` (min/avg/max/count line), `prices.store-line` (per-store line), `prices.unknown-store`
- [x] 7.2 Add same keys to `Strings.ru.json` with Russian translations
- [x] 7.3 Add same keys to `Strings.pl.json` with Polish translations

## 8. Dependency Injection

- [x] 8.1 Register `PriceLogRepository` as `AddScoped` in `Program.cs`
- [x] 8.2 Register `PricesCommandHandler` as `AddScoped<ICommandHandler, PricesCommandHandler>` in `Program.cs`

## 9. Tests — PriceLogRepository

- [x] 9.1 Test `AddAsync` saves all fields and returns record with non-zero `Id`
- [x] 9.2 Test `AddAsync` saves record with null `StoreName`
- [x] 9.3 Test `GetStatsAsync` returns correct min/avg/max/count for matching records
- [x] 9.4 Test `GetStatsAsync` is case-insensitive (query "milk" matches stored "Milk")
- [x] 9.5 Test `GetStatsAsync` is case-insensitive for Cyrillic ("молоко" matches "Молоко")
- [x] 9.6 Test `GetStatsAsync` returns null when no matching records exist
- [x] 9.7 Test `GetStatsAsync` scopes results to the group (records in another group not included)
- [x] 9.8 Test store breakdown includes per-store min/avg/max and is capped at 10 entries

## 10. Tests — SearchAsync Case-Insensitive Fix

- [x] 10.1 Test `SearchAsync` returns matches for Cyrillic query with different casing ("молоко" finds "Молоко")
- [x] 10.2 Test `SearchAsync` returns matches for Polish diacritics with different casing ("żółty" finds "Żółty")

## 11. Tests — PriceCaptureStepHandler Price Log Integration

- [x] 11.1 Test that completing price capture with non-null price calls `PriceLogRepository.AddAsync`
- [x] 11.2 Test that completing price capture with null price does NOT call `PriceLogRepository.AddAsync`
- [x] 11.3 Test that `PriceLogRepository.AddAsync` failure does not suppress user-facing confirmation

## 12. Tests — PricesCommandHandler

- [x] 12.1 Test that `/prices` in private chat replies with group-only message
- [x] 12.2 Test that `/prices` with no argument replies with `prices.usage`
- [x] 12.3 Test that `/prices <item>` with no data replies with `prices.not-found`
- [x] 12.4 Test that `/prices <item>` with data sends a reply containing min/avg/max/count
- [x] 12.5 Test that item name lookup is case-insensitive (query "milk" returns stats for "Milk")
