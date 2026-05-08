## Why

When users buy the same product repeatedly, prices paid are currently buried in the general `PurchaseHistory` audit log with no aggregation — users cannot easily tell what they typically pay for an item or track price trends. Additionally, item name lookups use raw SQLite `LIKE`, which silently fails for non-ASCII characters (Cyrillic, Polish diacritics), causing missed matches when query casing differs.

## What Changes

- New `PriceLog` table stores price-per-item entries separately from the general purchase audit trail, enabling clean price-focused queries without joining unrelated history data.
- `PriceLogRepository` provides `AddAsync` and `GetStatsAsync(groupId, itemName)` returning min, avg, and max price for the item (case-insensitive match).
- When price-capture dialog completes with a non-null price, a record is written to `PriceLog` in addition to (or instead of relying solely on) `PurchaseHistory`.
- New `/prices <item>` command shows price statistics for an item: min / avg / max price, number of observations, and breakdown by store.
- All item-name comparisons (price log lookup, purchase history search) use `LOWER()` in SQL to enforce case-insensitive matching for all Unicode characters, including Cyrillic and Polish diacritics.

## Capabilities

### New Capabilities
- `price-log`: Dedicated `PriceLog` table + `PriceLogRepository` with stats query (min/avg/max per item name, case-insensitive, per group).
- `price-stats-display`: `/prices <item>` command that returns formatted price statistics (min, avg, max, count, per-store breakdown) for a given item name.

### Modified Capabilities
- `purchase-history`: `SearchAsync` case-insensitive fix — replace raw `LIKE '%?%'` with `LOWER(ItemName) LIKE LOWER('%?%')` to handle non-ASCII characters correctly.

## Impact

- **Database**: New `PriceLog` table added in `DatabaseInitializer`; no existing tables altered.
- **Handlers**: New `PricesCommandHandler` implementing `ICommandHandler`; price-capture dialog completion path writes to `PriceLog`.
- **Repositories**: New `PriceLogRepository`; `PurchaseHistoryRepository.SearchAsync` query updated.
- **Localization**: New keys for `/prices` command responses in `Strings.{en,ru,pl}.json`.
- **DI**: `PriceLogRepository` and `PricesCommandHandler` registered in `Program.cs`.
- **Rollback**: `PriceLog` table can be dropped; `SearchAsync` query change is backward-compatible (returns same logical results for ASCII-only data). No breaking changes to existing commands.
- **Affected teams**: Backend only — no API surface changes.
- **AOT / cross-cutting**: `PriceLogRepository` uses raw `Microsoft.Data.Sqlite` (no Dapper/EF). All user-facing strings go through `ILocalizer`. History audit not required for `/prices` (read-only command). No reflection-based patterns introduced.
