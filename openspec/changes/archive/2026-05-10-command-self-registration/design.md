## Context

Telegram displays a command suggestion list when a user types `/`, but only after the bot has called `setMyCommands`. Currently no such registration occurs, so suggestions are empty. Handlers are already registered as `IEnumerable<ICommandHandler>` via DI; the only missing piece is surfacing per-handler metadata and pushing it to Telegram at startup.

Current architecture:
- `ICommandHandler` exposes `Command` (string) and `HandleAsync`
- `BotHostedService` starts the polling loop; no pre-flight API calls are made
- No central command list exists — handlers are discovered entirely through DI

## Goals / Non-Goals

**Goals:**
- Each `ICommandHandler` can optionally declare a `Description` (the text shown in Telegram's suggestion list)
- A startup service reads all `ICommandHandler` instances, collects those with descriptions, and calls `setMyCommands` once
- A `/start` handler sends a formatted welcome + command list to the user
- Adding a new handler with a description automatically appears in the suggestion list with zero extra wiring

**Non-Goals:**
- Per-user or per-group command scoping (`setMyCommands` scope parameter)
- Dynamic re-registration at runtime when handlers change
- Localized command descriptions sent to Telegram (Telegram `BotCommand.Description` is a single string; localization is only relevant in the `/start` reply message)

## Decisions

### Decision 1: Optional `Description` property on `ICommandHandler`

Add `string? Description { get; }` to `ICommandHandler`. Handlers that return `null` are excluded from `setMyCommands` and from the `/start` reply list. This is backwards-compatible — existing handlers implement the interface implicitly returning `null` via a default interface member, or they explicitly return a value.

**Alternative considered**: A separate `IBotCommandDescriptor` marker interface. Rejected — doubles the interface count for no gain; every command handler already knows its command string, co-locating the description is the natural place.

**Alternative considered**: A static attribute on the class. Rejected — incompatible with AOT (reflection-free requirement); DI-based enumeration is already the pattern in use.

### Decision 2: Separate `BotCommandRegistrationService` IHostedService

A dedicated `IHostedService` (not `BackgroundService`) calls `SetMyCommandsAsync` in `StartAsync` and then returns. This separates concerns: `BotHostedService` owns the polling loop, `BotCommandRegistrationService` owns the one-shot API registration.

Startup order: .NET hosted services start in registration order. `BotCommandRegistrationService` is registered before `BotHostedService`, so commands are registered before the bot begins receiving messages.

**Alternative considered**: Do it inside `BotHostedService.ExecuteAsync`. Rejected — `BotHostedService` blocks forever; mixing concerns makes testing harder.

### Decision 3: `StartCommandHandler` sends in-process command list

`/start` injects `IEnumerable<ICommandHandler>`, filters by non-null `Description`, and formats a reply message. The list is assembled at handler invocation time from the live DI container — same source of truth as the `setMyCommands` call.

Localization: The surrounding message text uses `ILocalizer`; individual command names and descriptions are plain strings (English only, matching what was sent to Telegram).

## Risks / Trade-offs

- [Risk] `setMyCommands` network call fails at startup → Mitigation: catch and log at Warning; polling loop starts regardless; suggestions will be stale but the bot remains functional
- [Risk] Handler added without description → it silently won't appear in suggestions → Mitigation: document the convention in `ICommandHandler`; the pattern is opt-in by design
- [Risk] Description text exceeds Telegram's 256-char limit per command → Mitigation: keep descriptions short (validated at code-review time; no runtime enforcement needed)

## Migration Plan

1. Deploy the new version — `setMyCommands` runs at startup and populates the suggestion list
2. Rollback: remove `BotCommandRegistrationService` and `StartCommandHandler` from `Program.cs`; `ICommandHandler.Description` can remain as a no-op nullable property with no runtime effect
