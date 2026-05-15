## Context

`UpdateDispatcher` is already unit-tested for routing logic (command prefix match, callback prefix match, dialog dispatch, error swallowing). What's missing is a test layer that exercises a full vertical slice: a real `Update` goes in, real handlers run against real SQLite data, and a real reply comes out to a mocked bot client.

The integration tests live in a new `Integration/` subdirectory of the existing test project. They share a base class that owns the wiring so individual test classes stay focused on scenarios.

## Goals / Non-Goals

**Goals:**
- Exercise the full `Update → UpdateDispatcher → Handler → Repository → ITelegramBotClient` path with no mocked intermediate layers
- Use real in-memory SQLite (shared-cache named connection per test class to avoid schema re-init cost)
- Mock only `ITelegramBotClient` (capture `SendRequest` calls for assertion)
- Cover three update types: command messages, callback queries, dialog messages
- Keep test setup readable — one base class, factory helpers for `Update` objects

**Non-Goals:**
- Testing long-polling, webhook deserialization, or Telegram HTTP transport
- Testing `BotHostedService` (it owns polling; not in scope)
- Full coverage of every handler (unit tests own that); integration tests cover representative flows per update type

## Decisions

### 1. Shared Base Class: `TelegramIntegrationTestBase`

**Decision**: A single abstract base class in `ProductTrackerBot.Tests/Integration/TelegramIntegrationTestBase.cs` owns:
- Opening a named in-memory SQLite connection (`file:integration_test?mode=memory&cache=shared`)
- Running `DatabaseInitializer.InitializeAsync()` once in the constructor
- Instantiating real repositories (`GroupRepository`, `ShoppingItemRepository`, `HistoryRepository`, etc.)
- Instantiating real services (`ShoppingListService`, `UndoService`, etc.)
- Instantiating real handlers
- Building the `UpdateDispatcher` with all those handlers
- Exposing `Mock<ITelegramBotClient> BotMock` for assertions
- Exposing `Update` builder helpers: `CommandUpdate(chatId, userId, text)`, `CallbackUpdate(chatId, userId, messageId, data)`, `MessageUpdate(chatId, userId, text)`
- Implementing `IDisposable` to close the SQLite connection

**Rationale**: Keeps individual test classes small. Each test class inherits the base, calls `await DispatchAsync(update)`, and asserts on `BotMock`. No repeated wiring code.

**Alternatives Considered**:
- xUnit `IClassFixture<T>` — more idiomatic for expensive shared setup, but makes per-test state isolation harder. Named in-memory connection with truncation between tests is simpler.
- Separate integration test project — unnecessary overhead; same `ProductTrackerBot.Tests` project is fine since no new NuGet packages are required.

### 2. Schema Initialization via `DatabaseInitializer`

**Decision**: Call `DatabaseInitializer.InitializeAsync(connection)` (passing the open `SqliteConnection`) in the base class constructor. This ensures the integration test schema is always in sync with production schema — no hand-written `CREATE TABLE` strings.

**Rationale**: Repository tests currently write their own `CREATE TABLE` SQL inline, which can drift from production. Using `DatabaseInitializer` directly avoids that drift and tests the initializer implicitly.

**Alternatives Considered**:
- Inline schema per test — already causes drift in existing repo tests; not repeating this
- Separate migration runner — overkill for SQLite with additive-only migrations

### 3. Per-Test Data Isolation

**Decision**: Each test method that writes data calls a helper `ClearDataAsync()` (exposed by base class) which `DELETE`s all rows from all tables at the start of the test (not between tests). Tests that only read pre-seeded data seed it in the test body.

**Rationale**: Recreating the connection per test is possible but slower. Truncation is fast and keeps the shared named connection open for the test class lifetime.

### 4. Bot Client: Mock Only `SendRequest`

**Decision**: `BotMock` is a `Mock<ITelegramBotClient>` with:
```csharp
BotMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(new Message());
BotMock.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync(new Message());
```
Assertions use `BotMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(...), ...), Times.Once)`.

**Rationale**: Matches the pattern already used in all existing handler unit tests. No extra infrastructure needed.

### 5. Update Builder Helpers

**Decision**: Static helpers on the base class using `JsonSerializer.Deserialize<Update>` (same `JsonOpts` as `UpdateDispatcherTests`):

```csharp
protected static Update CommandUpdate(long chatId, long userId, string text)
    => DeserializeUpdate($"{{\"update_id\":1,\"message\":{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"TestUser\"}},\"chat\":{{\"id\":{chatId}}},\"text\":\"{text}\"}}}}");

protected static Update CallbackUpdate(long chatId, long userId, int messageId, string data)
    => DeserializeUpdate($"{{\"update_id\":1,\"callback_query\":{{\"id\":\"cb1\",\"from\":{{\"id\":{userId},\"first_name\":\"TestUser\"}},\"message\":{{\"message_id\":{messageId},\"chat\":{{\"id\":{chatId}}}}},\"data\":\"{data}\"}}}}");

protected static Update MessageUpdate(long chatId, long userId, string text)
    => DeserializeUpdate($"{{\"update_id\":1,\"message\":{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"TestUser\"}},\"chat\":{{\"id\":{chatId}}},\"text\":\"{text}\"}}}}");
```

**Rationale**: Reuses the proven pattern from `UpdateDispatcherTests`. Keeps tests readable without raw JSON literals in every test method.

## Sequence Diagram

```
Test method
  │
  │  await DispatchAsync(CommandUpdate(-100, 42, "/buy Milk"))
  ▼
UpdateDispatcher.HandleUpdateAsync(BotMock.Object, update, ct)
  │
  │  routes to BuyCommandHandler
  ▼
BuyCommandHandler.HandleAsync(message, ct)
  │
  ├── GroupRepository.GetOrCreateAsync(-100)    [real SQLite]
  ├── ShoppingItemRepository.AddAsync(...)       [real SQLite]
  └── BotMock.SendRequest(SendMessageRequest)    [captured for assertion]
  │
  ▼
Test assertion:
  BotMock.Verify(b => b.SendRequest(
      It.Is<SendMessageRequest>(r => r.Text.Contains("Milk")), ...), Times.Once);
  // and/or: verify item exists in ShoppingItemRepository
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Schema drift between inline SQL and `DatabaseInitializer` | Use `DatabaseInitializer` directly — eliminates drift |
| Tests interfere with each other via shared SQLite connection | `ClearDataAsync()` at start of each test; deterministic cleanup |
| Handler wiring in base class becomes stale as handlers are added | Integration tests fail to compile if a new required constructor arg is missing — forces update |
| Named in-memory connection leaks between test runs | `IDisposable.Dispose()` closes connection explicitly in base class |
