## Context

`StartCommandHandler` currently enumerates all `ICommandHandler` instances that have a non-null `Description` and sends them as a formatted text message. The Telegram Bot API's `setMyCommands` endpoint is never called, so the "/" autocomplete menu in Telegram clients is empty.

`ITelegramBotClient` (from `Telegram.Bot`) exposes `SetMyCommandsAsync`, which accepts a list of `BotCommand` (text + description). This is a fire-and-forget idempotent call — calling it multiple times with the same list has no side-effects.

## Goals / Non-Goals

**Goals:**
- Call `setMyCommands` from `StartCommandHandler.HandleAsync` with all handlers that have a non-null `Description`.
- Keep the welcome message behaviour unchanged.
- Cover the new call in unit tests by verifying `SetMyCommandsAsync` is invoked with the correct commands.

**Non-Goals:**
- Calling `setMyCommands` on bot startup (outside of `/start`). The current approach is intentional: commands are registered lazily on first user interaction to avoid a startup dependency on Telegram connectivity.
- Scoped command menus (per-chat or per-user scope). Global registration only.
- Localizing command descriptions in the Telegram menu. The menu always shows the default locale description; in-chat messages remain localized.

## Decisions

### Where to place the `SetMyCommandsAsync` call

**Decision:** Inside `StartCommandHandler.HandleAsync`, immediately after sending the welcome message.

**Rationale:** The handler already has the full list of public commands, `ITelegramBotClient`, and cancellation token. Placing it here avoids a new abstraction. A failure to register commands should not prevent the welcome message from being delivered, so the call is wrapped in try/catch — log Warning and continue.

**Alternative considered:** A one-time startup registration in `BotHostedService`. Rejected because it adds a Telegram API call to the critical startup path; if Telegram is temporarily unreachable the bot fails to start.

### `SetMyCommandsAsync` vs `DeleteMyCommandsAsync` on empty list

**Decision:** Call `SetMyCommandsAsync` only when there is at least one public command. If no handlers have descriptions, skip the call (the empty-command welcome path already exists).

**Rationale:** Calling `SetMyCommandsAsync` with an empty array has the same effect as `DeleteMyCommandsAsync` but is semantically misleading. Keeping the guard consistent with the existing empty-command branch avoids ambiguity.

### Error handling

**Decision:** Wrap the `SetMyCommandsAsync` call in try/catch; log at Warning on failure; do not propagate the exception.

**Rationale:** Command registration failure is non-fatal — the welcome message was already delivered and the bot continues functioning. Propagating would surface an API outage as a user-visible error during `/start`.

## Risks / Trade-offs

- **Rate limiting**: Telegram enforces rate limits on Bot API calls. Multiple simultaneous `/start` invocations could hit limits. Mitigation: `setMyCommands` is idempotent and low-frequency; the risk is negligible.
- **Stale command menu**: If new commands are added to the bot code without restarting, the Telegram menu won't update until a user sends `/start`. Mitigation: acceptable for this project's scale; a startup registration can be added later if needed.
- **Test mock complexity**: `ITelegramBotClient` is an interface; `SetMyCommandsAsync` can be mocked with Moq without reflection concerns. AOT-safe.
