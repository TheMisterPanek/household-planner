## Context

The bot currently processes updates as pure fire-and-forget: commands execute, the database changes, and nothing records *what happened*. There is no audit trail, no way to undo the last action, and no data for future analytics. The project uses SQLite via raw `SqliteConnection` (no Dapper ORM), follows Clean Architecture with explicit DI, and must remain AOT-safe (no reflection, source-generated JSON).

## Goals / Non-Goals

**Goals:**
- Append a structured `BotActionHistory` row after every successful bot action (add item, buy item, remove item, view list, view history).
- Provide an `IHistoryRepository` abstraction so handlers depend on an interface, not a concrete class.
- Add a `/history` command that retrieves and displays the last 10 actions for the current chat.
- Keep existing handlers minimally modified — one async `RecordAsync` call added per handler.

**Non-Goals:**
- Undo/replay functionality (future capability).
- Cross-chat analytics or aggregation queries.
- Exporting or streaming history to an external system.
- Editing or deleting history entries.

## Decisions

### D1: Store history in SQLite alongside existing tables

**Chosen**: New `BotActionHistory` table in the same SQLite database, added in `DatabaseInitializer`.

**Alternatives considered**:
- Separate SQLite file — avoids any schema coupling but splits operational concerns and complicates backup.
- Structured log only (Serilog sink) — no query capability, can't power `/history` command.

**Rationale**: The existing database is already the single source of truth. Keeping history there means one backup, one connection string, and consistent transaction scope.

---

### D2: JSON payload as TEXT column, serialized with source-generated `System.Text.Json`

**Chosen**: `Payload TEXT` column holding a JSON string; serialization uses a `[JsonSerializable]` source-generated context.

**Alternatives considered**:
- Individual columns per action type — causes wide, sparse table and a migration for every new action.
- `System.Text.Json` with runtime reflection — breaks AOT/trimming.

**Rationale**: JSON-in-TEXT is a proven SQLite pattern for variable payloads. Source-generated serialization is already the project standard and is trimmer-safe.

---

### D3: `IHistoryRepository` with a single `RecordAsync` and a `GetRecentAsync`

**Chosen**: Interface with two methods:
```csharp
Task RecordAsync(long chatId, long userId, string userName, BotActionType actionType, string payloadJson, CancellationToken ct);
Task<IReadOnlyList<BotActionEntry>> GetRecentAsync(long chatId, int limit, CancellationToken ct);
```

**Rationale**: Minimal surface area. `RecordAsync` is append-only; `GetRecentAsync` powers `/history`. Both map directly to the table without additional abstraction layers.

---

### D4: `BotActionType` as an enum stored as TEXT

**Chosen**: `enum BotActionType { ItemAdded, ItemBought, ItemRemoved, ListViewed, HistoryViewed }` stored as its string name.

**Rationale**: Human-readable in the database without a lookup table. Extensible: adding a new enum member is a non-breaking schema change.

---

### D5: `/history` shows the last 10 entries for the current chat only

**Chosen**: Fixed limit of 10, scoped to `ChatId`.

**Rationale**: Keeps the Telegram message short (fits in one message). Privacy — users only see their own group's history.

## Risks / Trade-offs

- **Write amplification** → Every handler call now makes an additional INSERT. Risk: latency spike under high load. Mitigation: SQLite writes are sub-millisecond; fire-and-forget pattern is already used. No buffering needed at current scale.
- **History grows unbounded** → No TTL or pruning. Mitigation: at typical household bot usage this is negligible. An optional cron-based prune can be added later without schema changes.
- **AOT JSON serialization** → If a new payload type is added without updating the `[JsonSerializable]` context, it will fail at runtime. Mitigation: unit tests must serialize every `BotActionPayload` variant; the source-generated context lists all types explicitly.
- **Handler error path** → If `RecordAsync` throws, does it bubble up and suppress the original bot response? Decision: wrap in `try/catch` inside the handler — log and swallow history errors so the primary action always completes.

## Migration Plan

1. Add `BotActionHistory` DDL to `DatabaseInitializer.StartAsync` using `CREATE TABLE IF NOT EXISTS` — safe on existing databases.
2. Register `IHistoryRepository` / `HistoryRepository` in `Program.cs`.
3. Register `HistoryCommandHandler` in the dispatcher.
4. Inject and call `IHistoryRepository` in each handler.
5. **Rollback**: Drop `BotActionHistory` table and revert handler injections. No data loss to existing tables.

## Open Questions

- Should `/history` be available in private chats (showing the user's actions across all groups) or group-only? *Assumed group-only for now, consistent with other commands.*
- Should history entries record the `GroupId` (internal FK) or only `ChatId` (Telegram ID)? *Chosen `ChatId` (Telegram ID) to avoid the Groups-table FK lookup on every write — simpler and sufficient for querying.*
