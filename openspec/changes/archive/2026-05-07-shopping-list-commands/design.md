## Context

The bot has a single `UpdateHandler` stub that receives all Telegram updates and does nothing. There is no persistence layer, no command routing, and no multi-step dialog state. This design introduces all three for the shopping list feature.

Current constraints:
- AOT compilation (`PublishAot=true`) — no runtime reflection, no `dynamic`, source generators only
- `Microsoft.Extensions.Hosting` DI already wired
- Single-project layout (`ProductTrackerBot/`)
- Russian-language UI for family Telegram group

## Goals / Non-Goals

**Goals:**
- Route incoming commands to typed handler classes
- Persist shopping list items in SQLite (survives restarts)
- `/buy` two-step dialog: ask name, then ask quantity (with skip button)
- `/list` posts a live-edited message showing all items with action buttons
- Mark-bought and remove-item inline button handlers
- Auto-register a `Group` row on first command in a chat

**Non-Goals:**
- EF Core migrations (raw SQL schema init only)
- Multi-step inventory flow (buy→stock quick-add — Phase 3)
- Multi-group support (single `ChatId` per v1 instance)
- Tests project restructure (single project for now; tests added as separate project in a later phase)

## Decisions

### D1: Raw `Microsoft.Data.Sqlite` over EF Core

**Decision:** Use raw ADO.NET via `Microsoft.Data.Sqlite` instead of EF Core.

**Rationale:** EF Core + AOT requires source-generated `DbContext`, suppressed trim warnings, and a dedicated migration bundler step — significant overhead for a schema with 2–3 tables. Raw SQL with `SqliteConnection` is AOT-native with no reflection. At household scale (tens of rows) there is no performance difference.

**Alternative considered:** EF Core with `SuppressTrimAnalysisWarnings=true` — works but adds ~5 NuGet packages, build complexity, and fragile AOT trim hints.

### D2: `UpdateDispatcher` replaces `UpdateHandler` as the DI-registered `IUpdateHandler`

**Decision:** Register `UpdateDispatcher : IUpdateHandler` in DI. It resolves all `ICommandHandler` and `ICallbackHandler` implementations from the container and routes by command text / callback prefix.

**Rationale:** Keeps `Program.cs` clean; each feature adds only a handler registration. No reflection-based auto-discovery (AOT-safe) — handlers are registered explicitly with `AddScoped<ICommandHandler, BuyCommandHandler>()`.

```
Telegram Update
      │
      ▼
UpdateDispatcher
  ├── Message update → match update.Message.Text prefix "/" → ICommandHandler.Command
  │       └── /buy  → BuyCommandHandler
  │       └── /list → ListCommandHandler
  │
  └── CallbackQuery → match data prefix → ICallbackHandler.CallbackPrefix
          └── "shop:done:"   → ShopDoneCallbackHandler
          └── "shop:remove:" → ShopRemoveCallbackHandler
```

### D3: `PendingDialogService` as a singleton `ConcurrentDictionary`

**Decision:** Track multi-step dialog state in memory (not SQLite).

**Rationale:** Dialog state is ephemeral — losing it on restart (rare) just means the user retypes `/buy`. Persisting it adds schema complexity for no real gain. Keyed by `(chatId, userId)` so two users can be in `/buy` dialog simultaneously.

### D4: Live list message edited in-place

**Decision:** `/list` posts a new message and saves `ListMessageId` in the `Group` table. Subsequent `/list` calls and any item change edit that same message via `EditMessageTextAsync`. If Telegram returns a 400 (48h edit limit), repost and update `ListMessageId`.

**Rationale:** Matches the spec; avoids message spam in the group chat.

### D5: SQLite schema initialized at startup with `CREATE TABLE IF NOT EXISTS`

**Decision:** `DatabaseInitializer` runs at `IHostedService.StartAsync` and executes DDL inline.

**Rationale:** No migration runner needed. Schema is append-only in v1; breaking changes will be handled by a future migration strategy when EF Core is adopted (if ever).

```sql
CREATE TABLE IF NOT EXISTS Groups (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChatId INTEGER NOT NULL UNIQUE,
    ListMessageId INTEGER
);

CREATE TABLE IF NOT EXISTS ShoppingItems (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GroupId INTEGER NOT NULL REFERENCES Groups(Id),
    Name TEXT NOT NULL,
    Quantity TEXT,
    AddedByName TEXT NOT NULL
);
```

## Risks / Trade-offs

- **In-memory dialog state lost on restart** → Acceptable; user retypes `/buy`. Mitigation: bot sends "Диалог сброшен, попробуйте снова" if dialog state is missing on step 2.
- **Telegram 48h edit limit on list message** → Handled: catch `ApiRequestException` with 400 status, repost and save new `MessageId`.
- **Concurrent `/buy` dialogs from same user** → Not a risk; keyed by `(chatId, userId)`.
- **Single-project layout limits testability** → Tests project deferred; core logic in `Services/` classes (not handlers) will be easy to extract later.
- **Raw SQL with string concatenation = injection risk** → Mitigation: use parameterized queries (`SqliteParameter`) everywhere. No string interpolation in SQL.

## Migration Plan

1. Add `Microsoft.Data.Sqlite` NuGet reference
2. Add `DatabaseInitializer` hosted service (runs DDL before bot starts polling)
3. Replace `UpdateHandler` registration with `UpdateDispatcher`
4. Add handler and service registrations to `Program.cs`
5. Deploy as normal Docker build — SQLite file on named volume persists between deployments

**Rollback:** Revert `Program.cs` DI registrations, restore `UpdateHandler` stub, remove new files. No schema rollback needed (bot is stateless without the handlers).

## Open Questions

- Should `/list` be callable in a private chat (shows redirect message) or silently ignored? → Default to redirect message per spec.
- Max item name length? → Not enforced in v1; Telegram message length is the practical limit.
