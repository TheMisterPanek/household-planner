## Why

The bot has no memory of what it has done in a chat — every action is fire-and-forget. A history register gives each group chat a persistent, ordered record of bot actions so users can audit what happened, and future features (undo, analytics, replay) have a foundation to build on.

## What Changes

- Introduce a `BotActionHistory` table in SQLite that logs every significant bot action (command handled, item added, item bought, item removed) with timestamp, chat ID, user ID, and a JSON payload.
- Expose a new `IHistoryRepository` abstraction and `HistoryRepository` implementation backed by that table.
- Inject `IHistoryRepository` into each existing handler; handlers record an entry after each successful action.
- Add a `/history` command that prints the last N actions for the current chat.

## Capabilities

### New Capabilities
- `bot-action-history`: Persistent per-chat log of bot actions (add-item, buy-item, remove-item, list-viewed, history-viewed). Provides `IHistoryRepository` with append and query operations. Powers the `/history` command.

### Modified Capabilities
- `shopping-list`: Handlers for `/buy`, list-done, list-remove, and `/list` now write a history entry after each successful state change.

## Impact

- **Database**: New `BotActionHistory` migration added to `Database/` setup.
- **Repositories**: New `HistoryRepository` added to `Repositories/`.
- **Handlers**: `BuyCommandHandler`, `BuyStepHandler`, `BuySkipCallbackHandler`, `ShopDoneCallbackHandler`, `ShopRemoveCallbackHandler`, `ListCommandHandler` each receive an `IHistoryRepository` dependency.
- **Commands**: New `HistoryCommandHandler` implementing `ICommandHandler`.
- **DI**: `IHistoryRepository` / `HistoryRepository` registered in `Program.cs`; new handler registered in dispatcher.
- **Rollback plan**: The `BotActionHistory` table is additive; dropping it restores previous state. Handler changes are isolated to a single `await _history.RecordAsync(...)` call that can be removed without other side effects.
- **Affected teams**: Solo project — no cross-team impact.
- **AOT compatibility**: `HistoryRepository` uses Dapper with trimmer-safe patterns already established in the project; JSON payload serialized with `System.Text.Json` source-generated context.
