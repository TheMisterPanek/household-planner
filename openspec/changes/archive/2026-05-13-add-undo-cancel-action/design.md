## Context

Currently, every destructive inline-button action (mark item done, archive list, delete item) executes immediately on tap with no confirmation or recovery path. Users misclick and lose state. The history table already records actions but carries no revert payload, so programmatic undo is not possible today.

This design adds two independent but related safeguards:
1. **Cancel button** — pre-commit: the action message includes a ⬅️ Cancel button; pressing it aborts without executing.
2. **/undo command** — post-commit: replays the inverse of the most recent reversible history entry.

## Goals / Non-Goals

**Goals:**
- Cancel button on every destructive inline action (mark done, archive, delete, merge)
- `/undo` command that reverts the last reversible action for the issuing user in that chat
- Single-level undo only (no redo, no multi-step undo stack)
- All revert payloads AOT-safe (source-generated JSON)

**Non-Goals:**
- Redo / redo stack
- Undo of `/history` or `/start` (read-only; nothing to revert)
- Undo across chats (scope is per chat + per user)
- Persistent undo queue beyond the most recent entry

## Decisions

### Cancel button placement
**Decision**: Add a Cancel button as an extra row at the bottom of any `InlineKeyboardMarkup` for destructive actions. The Cancel callback uses prefix `action:cancel:<messageId>` and simply calls `DeleteMessageAsync` (or `EditMessageTextAsync` to remove the keyboard if deletion is forbidden).

**Why over a confirmation dialog**: A two-step confirm pattern doubles the taps required for normal use. A Cancel button on the action message is lower friction — it only costs extra if you regret.

**Alternative considered**: Modal confirmation (send new message asking "Are you sure?"). Rejected because it clutters the chat and requires an extra tap on the happy path.

### Revert payload storage
**Decision**: Add a nullable `revert_payload TEXT` column to `BotActionHistory`. The column stores a JSON object typed by `action_type`, serialized via the existing source-generated `BotJsonContext`. A new `BotActionType` values (`ListItemReverted`, `ListArchiveReverted`, etc.) marks already-reverted entries.

**Why not a separate undo table**: Keeping revert state in the history row avoids a join and a second table. The existing audit trail already owns "what happened"; the inverse op is just metadata on that fact.

**Alternative considered**: In-memory undo stack (singleton service). Rejected because it is lost on restart; history-backed undo survives deploys.

### Undo scope
**Decision**: `GetLatestReversibleAsync(chatId, userId)` — scoped to the combination of chat + user who issued `/undo`. A user cannot undo another user's action.

**Why**: Prevents cross-user interference in group chats. Aligns with user expectation ("I want to undo *my* last thing").

### UndoService dispatch
**Decision**: `UndoService` reads the latest reversible history entry, deserializes `revert_payload` into a discriminated `RevertOperation` sealed class hierarchy (one subclass per action type), then calls the appropriate repository method. It does NOT go through `UpdateDispatcher` — it calls repos directly.

**Why**: Routing through the dispatcher would require fabricating a fake `Update`, which is fragile. Direct repo calls are simpler, testable, and already the pattern in other service-layer code.

### Callback data size
Cancel button callback: `action:cancel` (12 bytes) — well under the 64-byte limit. No session token needed.

## Risks / Trade-offs

- [Revert payload schema drift] If an action's data model changes after a history row is written, old `revert_payload` JSON may fail to deserialize. → Mitigation: `UndoService` wraps deserialization in try/catch; on failure it replies with localized "cannot undo this action" and logs a Warning.
- [Group delete permissions] Bots cannot always delete messages in groups. → Mitigation: Cancel button handler calls `DeleteMessageAsync` and falls back to `EditMessageReplyMarkupAsync(null)` (removes keyboard) if deletion throws `ApiRequestException`.
- [Race condition] Two users undo simultaneously. → Acceptable for single-level undo: last write wins; no locking needed.
- [History table growth] `revert_payload` is nullable TEXT, so existing rows and non-invertible actions store NULL. Zero cost for rows that don't need it.

## Migration Plan

1. `DatabaseInitializer.StartAsync` gains the `PRAGMA table_info` + `ALTER TABLE bot_action_history ADD COLUMN revert_payload TEXT` block (existing pattern).
2. All existing history rows have `revert_payload = NULL` — treated as non-reversible; `/undo` will report "nothing to undo" until new actions are recorded.
3. Rollback: drop the handler registration and remove the `ALTER TABLE` block. The column is nullable so existing code ignores it safely.

## Open Questions

- Should Cancel button be available on non-destructive actions (e.g., view item details)? → Decision deferred; initial scope is destructive-only (mark done, archive, delete, merge).
- Should `/undo` be group-only or also work in private chats? → Available in both; in private chats, user ID == chat ID so scoping works naturally.
