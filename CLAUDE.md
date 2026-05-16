# Household Planner — Claude Code Guide

## Quick start

```bash
make build    # Compile the project
make run      # Start the bot (requires BOT_TOKEN in .env)
make test     # Run test suite
```

Bot requires a Telegram token. Set via `BOT_TOKEN` environment variable or `.env` file.

## Architecture

**Core layers:**
- **Handlers** — Parse Telegram updates; routed by command, callback type, or dialog state
- **Services** — Business logic (ShoppingListService, MealMergeService)
- **Repositories** — SQLite data access; SQLite in-memory for tests
- **Localization** — Per-chat language preferences; English, Russian, Polish

**Update flow:** `BotHostedService` → `UpdateDispatcher` → `ICommandHandler` / `ICallbackHandler` / `IDialogMessageHandler`

## Rules

### Test requirements
- Every handler and service change must include unit tests
- Mocks are preferred over real database in tests (exception: integration tests explicitly requiring SQLite)
- Test mocks for `ILocalizer` should return key names as fallback (allows assertions on key names)
- Use `Mock.Of<T>()` for simple throwaway mocks; `new Mock<T>()` when setup is needed

### Integration test requirements
- Every new command handler, callback handler, or dialog flow must have a corresponding integration test in `ProductTrackerBot.Tests/Integration/`
- Integration tests use `TelegramIntegrationTestBase` (named in-memory SQLite, mocked `ITelegramBotClient`)
- All integration test classes carry `[Collection("IntegrationTests")]` so they run serially and share no state
- Each test calls `ClearDataAsync()` at the start to ensure isolation
- If adding a new handler, wire it into `TelegramIntegrationTestBase` so it is available to all integration tests
- Integration tests verify the full update dispatch path: update in → bot mock call captured → repository state changed

### TDD approach
- Write the integration test for new features before implementing them; unit tests may be written alongside
- Use failing tests to define the expected contract of a new handler or service
- Keep test setup minimal — seed only the data each test needs

### Dependency injection
- All repositories and services must be registered in `Program.cs`
- Use `AddScoped` for stateless services and repositories
- Use `AddSingleton` for stateless immutable services (Localizer, IHistoryRepository impl)
- Dialog state services are `AddSingleton` (PendingDialogService<T>)

### Code style
- Handler command strings match Telegram commands exactly (e.g., `"/meals"`)
- Callback data format: `namespace:action:param1:param2` (e.g., `shop:done:5`, `meal:view:10`)
- Localization keys use dot-notation (e.g., `"list.header"`, `"buy.what-to-buy"`)
- All user-facing messages use localizer: `localizer.Get(chatId, key)`

### Build and run
- `dotnet build` must succeed with 0 errors before committing
- Pre-existing warnings (nullability, StyleCop) are acceptable; do not add new warnings
- `make run` must start the bot without dependency resolution errors
- All handler dependencies must be wired in `Program.cs`

### Database
- SQLite schema changes go in `DatabaseInitializer.InitializeAsync()`
- Migrations are additive only (no dropping columns)
- Tests use in-memory SQLite (`:memory:` connection string)

## OpenSpec integration

When proposing features:

1. **Scope**: Use `/openspec-propose` to create a change with design docs and task breakdown
2. **Unit tests**: Every handler/service proposal must include explicit test tasks
3. **Implementation**: Use `/openspec-apply-change` to work through tasks
4. **Smoke test**: After implementation tasks, always append a manual e2e smoke test task as the final task in the breakdown. This task describes how to verify the feature end-to-end in a real Telegram chat (commands to send, expected bot responses, edge cases to check by hand). Claude must stop and leave this task open — do NOT mark it complete or proceed to archive until the user confirms the smoke test passed.
5. **Landing page**: Every major feature must include a task to update the Landing page with a description of the new feature
6. **Archive**: When complete, use `/openspec-archive-change` to finalize

Example:
```
User: /openspec-propose Add /history command to view user activity log

→ Creates spec with:
  - Design doc (how pagination works, what data is shown)
  - Implementation tasks (handler, repository query, tests)
  - Test tasks (covers pagination, filtering, error cases)
  - Smoke test guide (send /history, verify paginated list appears, test empty state, test with 1 item)
```

## Files to know

| File | Purpose |
|---|---|
| `Program.cs` | DI registration; must register all handlers and repos here |
| `UpdateDispatcher.cs` | Routes updates to appropriate handler; add new handler types here |
| `Handlers/` | Command, callback, and dialog step handlers |
| `Repositories/` | Data access; repositories own schema changes |
| `Localization/Strings.{en,ru,pl}.json` | Translation keys; add keys here before using |
| `Models/` | Payloads for callback data; use `JsonSerializer` with snake_case policy |

## Common tasks

### Add a new command handler

1. Create `Handlers/MyCommandHandler.cs` implementing `ICommandHandler`
2. Register in `Program.cs`: `builder.Services.AddScoped<ICommandHandler, MyCommandHandler>();`
3. Add tests in `ProductTrackerBot.Tests/Handlers/MyCommandHandlerTests.cs`
4. Add localization keys to `Strings.*.json`

### Add a new callback

1. Update callback data format: `"namespace:action:param"`
2. Add handler in existing callback handler or create new one
3. Parse payload via `JsonSerializer.Deserialize<PayloadType>(...)`
4. Add tests covering valid/invalid data

### Add localization key

1. Add key to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`
2. Use in code: `localizer.Get(chatId, "your.new.key")`
3. Key format: `feature.context.variant` (e.g., `"list.empty"`, `"buy.group-only"`)
