## Why

Existing tests are pure unit tests — every layer beyond the class under test is mocked. This means the full pipeline (`Update arrives → UpdateDispatcher routes → Handler runs → Repository writes → Bot sends message`) is never exercised together. Wiring bugs (wrong DI registration, handler reading from the wrong repository method, callback prefix mismatch) can only be caught manually by running the bot against real Telegram.

Mocked integration tests fill this gap: they construct real `Update` objects, feed them into a real `UpdateDispatcher` wired to real handlers and real in-memory SQLite repositories, and assert on the messages sent to a mocked `ITelegramBotClient`. No long polling, no HTTP, no Telegram account needed.

## What Changes

- **`TelegramIntegrationTestBase`**: New shared fixture base class in `ProductTrackerBot.Tests/Integration/`. Spins up an in-memory SQLite database with the full schema (via `DatabaseInitializer`), creates real repository instances, creates real service instances, creates real handlers, assembles a real `UpdateDispatcher`, and mocks only `ITelegramBotClient`. Provides `Update`-building helpers for commands, callbacks, and plain messages.
- **Command integration tests**: Cover `/buy`, `/list`, `/start` end-to-end — from `Update` construction through handler execution to captured `SendMessageRequest` assertions and actual database state.
- **Callback integration tests**: Cover `shop:done` and `shop:remove` callbacks — verify that the correct repository mutations happen and the bot sends the expected reply.
- **Dialog state integration tests**: Cover multi-step dialog flows (e.g., price capture) — feed a sequence of messages through the dispatcher and verify state transitions and final bot replies.

## Capabilities

### New Capabilities

- `mocked-integration-tests`: End-to-end test coverage for the full Update → Dispatcher → Handler → Repository → Bot pipeline using in-memory SQLite and a mocked `ITelegramBotClient`.

## Impact

- **Code**: New test files only; no changes to production code
- **APIs**: No external API calls
- **Dependencies**: No new dependencies (Moq and Microsoft.Data.Sqlite already in test project)
- **Systems**: No schema changes; in-memory SQLite only
- **Compatibility**: Tests are fully self-contained; existing tests unaffected

## Rollback Plan

Test-only change. Rolling back means deleting the new files under `ProductTrackerBot.Tests/Integration/`. No production code is touched.

## Affected Teams

Single-user/household bot. No shared infrastructure impact.

## Cross-Cutting Notes

- Uses the same `JsonSerializerOptions` (snake_case) already established in `UpdateDispatcherTests` for deserializing `Update` objects
- `DatabaseInitializer` is called in fixture setup to guarantee schema parity with production
- Handler dependencies (localizer, logger) use the same mock conventions as existing handler tests: `ILocalizer` returns key names as fallback
