## Why

Users misclick Telegram inline buttons and trigger unintended actions (marking items done, archiving lists, etc.) with no way to recover. A `/undo` command and per-action Cancel buttons address both the accidental-tap problem and provide a discoverable single-level undo path.

## What Changes

- Add `/undo` Telegram command that reverts the most recent reversible action for the calling user in the current chat
- Attach a **Cancel** inline button to any message that triggers a destructive or hard-to-reverse action (mark done, archive, delete, merge)
- Cancel button dismisses the action before it is committed (pre-confirmation flow) — it does not invoke undo
- Undo is a post-commit revert: it reads the latest history entry and applies the inverse operation
- No redo capability — single-level undo only

## Capabilities

### New Capabilities
- `undo-command`: `/undo` command handler that reads the most recent reversible history entry for the user/chat and applies its inverse; replies with a localized confirmation or "nothing to undo" message
- `action-cancel-button`: Inline Cancel button added to action-confirmation messages; pressing it deletes or edits the message without executing the action

### Modified Capabilities
- `bot-action-history`: History entries must carry enough data to invert the action (snapshot of pre-change state or typed operation descriptor); the `action_type` field and a new `revert_payload` column are required for undo to work

## Impact

- **Handlers**: All destructive `ICallbackHandler` implementations gain a Cancel button option in their keyboard; `/undo` is a new `ICommandHandler`
- **Repositories**: `IHistoryRepository` gets a `GetLatestReversibleAsync(chatId, userId)` query; `bot_action_history` table gains a `revert_payload` TEXT column (nullable, JSON)
- **Services**: New `UndoService` orchestrates: fetch history → deserialize `revert_payload` → dispatch inverse operation → mark entry as reverted
- **Localization**: New keys for cancel button label, undo success, undo nothing-to-undo, undo already-reverted
- **Rollback plan**: Feature is purely additive — removing it means dropping the `/undo` handler, the Cancel button from keyboards, and the `revert_payload` column (nullable, so safe to ignore without migration)
- **AOT**: `revert_payload` deserialization must use the existing source-generated `JsonSerializerContext`; no reflection-based JSON
- **Cross-cutting deviations**: None — follows existing `IHistoryRepository.RecordAsync` audit pattern, localization rules, and callback data conventions
