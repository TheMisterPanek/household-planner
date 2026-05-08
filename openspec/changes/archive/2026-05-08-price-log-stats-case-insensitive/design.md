## Context

The current `PurchaseHistory` table mixes price data with general purchase audit records (who bought what, when, where). There is no dedicated path for querying price statistics per product. Additionally, `SearchAsync` uses raw `LIKE` without `LOWER()`, which is unreliable for non-ASCII characters (Cyrillic, Polish diacritics) — both the query term and stored value must be lowercased in SQL to guarantee Unicode case-insensitive matching in SQLite.

This change introduces a focused `PriceLog` table, a `PriceLogRepository` with stats queries, a `/prices` command handler, and a one-line fix to `SearchAsync`.

## Goals / Non-Goals

**Goals:**
- Dedicated `PriceLog` table storing one row per price observation (item + price + store + date)
- Stats query: min / avg / max price per item name (case-insensitive), per group
- Per-store breakdown in stats response
- `/prices <item>` command surfacing those stats in Telegram
- Case-insensitive fix in `PurchaseHistoryRepository.SearchAsync`

**Non-Goals:**
- Replacing `PurchaseHistory` — `PriceLog` is additive; existing audit trail is unchanged
- Price trend charts or time-series views
- Global (cross-group) price statistics
- Migrating historical `PurchaseHistory` price data into `PriceLog`

## Decisions

### Decision 1: New `PriceLog` table, not a view over `PurchaseHistory`

**Chosen**: New table `PriceLog(Id, GroupId, ItemName, Price REAL NOT NULL, StoreName TEXT, LoggedAt TEXT)`.

**Why**: `PurchaseHistory.Price` is nullable and semantically belongs to the audit trail. Querying it for stats requires filtering nulls and risks coupling the price feature to audit schema. A dedicated table keeps the price concern isolated and lets the stats query be simple.

**Alternative considered**: Add a `GetPriceStatsAsync` method directly to `PurchaseHistoryRepository` filtering non-null prices. Rejected because it conflates two concerns and makes future changes (e.g., logging a price without a full purchase record) awkward.

### Decision 2: Write to `PriceLog` from price-capture completion, not via a domain event

**Chosen**: `PriceCaptureStepHandler` calls `PriceLogRepository.AddAsync` directly when price is non-null, after calling `PurchaseHistoryRepository.AddAsync`.

**Why**: No event bus exists; the handler already knows the price at completion. Adding a thin direct call is consistent with how `IHistoryRepository.RecordAsync` is called — fire-and-forget wrapped in try/catch, never suppresses the user response.

### Decision 3: Case-insensitive matching via `LOWER()` in SQL

**Chosen**: `WHERE LOWER(ItemName) LIKE LOWER('%' || @query || '%')` for both `SearchAsync` and price stats lookup.

**Why**: SQLite's built-in `LIKE` is case-insensitive only for ASCII A–Z. For Cyrillic or Polish characters, `LOWER()` applied to both sides gives correct Unicode folding. No extension functions or ICU library required.

**Alternative considered**: `COLLATE NOCASE` — ASCII-only, same limitation as raw `LIKE`.

### Decision 4: `/prices` is group-only, no pagination

**Chosen**: Command requires a group chat context; returns up to 10 stores in the breakdown. Reply is a single text message, no inline buttons.

**Why**: Price data is per-group. Pagination adds complexity; 10 stores covers realistic usage. Stats fit in a single message well within Telegram limits.

## Risks / Trade-offs

- **Dual-write inconsistency**: If the `PriceLog.AddAsync` call fails after `PurchaseHistory.AddAsync` succeeds, the price is in audit log but missing from stats. Mitigation: wrap in try/catch, log Warning, proceed — same pattern used for history audit writes. Acceptable because stats are advisory.
- **No backfill**: Historical prices from `PurchaseHistory` are not migrated to `PriceLog`. Stats reflect only purchases made after this change ships. Mitigation: documented non-goal; could be added later via a one-time migration job.
- **LOWER() performance**: For very large `PriceLog` tables, `LOWER(ItemName)` prevents index usage. Mitigation: acceptable at current scale (typical group has hundreds of entries, not millions). A functional index (`CREATE INDEX ... ON PriceLog(LOWER(ItemName))`) can be added if needed.

## Migration Plan

1. `DatabaseInitializer.StartAsync` creates `PriceLog` with `CREATE TABLE IF NOT EXISTS` — safe on existing databases.
2. No existing columns or tables are altered.
3. `SearchAsync` query change is backward-compatible (returns same results for ASCII-only data, fixed for non-ASCII).
4. Rollback: drop `PriceLog` table (no data loss to other tables); revert `SearchAsync` query.
