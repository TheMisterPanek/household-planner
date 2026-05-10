## Why

Telegram shows command suggestions when users type `/`, but only if the bot has registered its commands via `setMyCommands`. Currently there is no such registration, so users must discover commands by guessing or reading docs. A reverse-dependency pattern (each handler declares its own command metadata) keeps the list accurate without requiring a central registry to be updated manually every time a handler is added or removed.

## What Changes

- Introduce an `IBotCommand` interface (or marker property on `ICommandHandler`) so each handler can expose its command name and description
- Add a `BotCommandRegistrationService` (hosted service) that collects all registered commands at startup and calls `setMyCommands` on the Telegram API
- Add a `/start` handler that sends a formatted list of available commands to the user as a welcome/help message
- Localize command descriptions through the existing `ILocalizer` pipeline

## Capabilities

### New Capabilities

- `bot-command-self-registration`: Each `ICommandHandler` optionally exposes command metadata (`/name` + description); a startup service collects these and pushes them to Telegram's `setMyCommands`, so the `/` suggestion list is always in sync with deployed handlers

### Modified Capabilities

- `telegram-bot-core`: Dispatcher must tolerate `/start` as a valid command route; startup sequence gains a `setMyCommands` step after handlers are resolved

## Impact

- **Code**: `ICommandHandler` gains an optional metadata surface; `BotHostedService` or a new `IHostedService` calls Telegram at startup; new `StartCommandHandler`
- **No breaking changes**: handlers that don't expose metadata are silently excluded from the `setMyCommands` payload
- **Rollback**: remove `BotCommandRegistrationService` registration and `StartCommandHandler`; existing handlers are unaffected
- **AOT**: No reflection — commands are resolved through DI-injected `IEnumerable<ICommandHandler>`; no runtime type scanning
- **Cross-cutting deviations**: none — follows existing DI handler registration and `IHostedService` patterns exactly
