## Why

Telegram shows command suggestions when a user types "/" only if the bot has registered its commands via `setMyCommands`. Currently the bot sends a welcome message listing commands as plain text, but never calls `setMyCommands`, so the "/" autocomplete menu remains empty and users have no discoverable way to invoke commands.

## What Changes

- On `/start`, after sending the welcome message, call `ITelegramBotClient.SetMyCommands` with all handlers that expose a `Description`.
- Commands are registered globally (no scope restriction), so the menu is available in any chat where the bot is present.
- No new commands are added; no existing commands are removed or renamed.

## Capabilities

### New Capabilities

- `bot-command-registration`: Registers bot commands with the Telegram Bot API (`setMyCommands`) so the "/" autocomplete menu is populated for users.

### Modified Capabilities

- `telegram-bot-core`: The `/start` handler gains a side-effect — it now calls `setMyCommands` in addition to sending the welcome message.

## Impact

- **`StartCommandHandler`** — gains a `SetMyCommands` call after the welcome message.
- **`ICommandHandler`** — no interface changes; `Description` property already drives both the welcome list and the new registration.
- **Telegram Bot API** — one additional API call per `/start` invocation; no new permissions required.
- **Tests** — `StartCommandHandlerTests` must verify that `SetMyCommands` is called with the correct command list.
- **Rollback** — removing the `SetMyCommands` call reverts to the previous behaviour; Telegram clears the command menu within minutes after the next `deleteMyCommands` or the bot is removed from the chat.
