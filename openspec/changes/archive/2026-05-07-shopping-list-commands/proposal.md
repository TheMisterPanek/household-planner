## Why

The bot scaffold is complete but responds to nothing. Without `/buy` and `/list`, the bot has no user-facing value. These two commands are the core daily-use loop: family members add items to buy, and the shared list is always visible in the group chat.

## What Changes

- Add `/buy` command: 2-step dialog (name → optional quantity) that appends an item to the shared shopping list
- Add `/list` command: posts the current shopping list as a Telegram message with inline buttons
- Add inline buttons per list item: `[✓ Name qty]` to mark bought, `[✗ Убрать]` to remove without buying
- Add SQLite persistence via `Microsoft.Data.Sqlite` (raw SQL, AOT-safe) — first persistent storage in this project
- Add `UpdateDispatcher` to route commands and callback queries to handlers
- Add `PendingDialogService` for multi-step command state (`/buy` dialog)
- Wire group auto-registration: first command in a chat creates the `Group` row

## Capabilities

### New Capabilities
- `shopping-list`: Add items, view the shared list, mark bought, remove items via inline buttons

### Modified Capabilities
- `update-handling`: `UpdateHandler` currently stubs all messages; will be replaced by a dispatcher that routes to command handlers and callback handlers

## Impact

- **Code**: `UpdateHandler.cs` replaced by `UpdateDispatcher`; new handler classes; new SQLite layer
- **Dependencies**: Add `Microsoft.Data.Sqlite` NuGet package
- **AOT**: Raw SQL with `Microsoft.Data.Sqlite` is AOT-safe; no EF Core reflection required
- **Rollback**: Remove new handler registrations from `Program.cs` and delete handler/DB files — `UpdateHandler.cs` stub can be restored
- **Teams**: Single developer
