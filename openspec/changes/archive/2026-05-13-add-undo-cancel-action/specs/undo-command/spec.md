## ADDED Requirements

### Requirement: /undo command reverts the last reversible user action
The system SHALL register an `UndoCommandHandler` for the `/undo` command. When invoked, it SHALL call `UndoService.UndoLastAsync(chatId, userId)` which fetches the most recent history entry for that chat + user where `revert_payload IS NOT NULL` and `reverted_at IS NULL`, applies the inverse operation via the relevant repository, marks the history row as reverted, and replies with a localized success message. If no reversible entry exists, it SHALL reply with a localized "nothing to undo" message.

#### Scenario: User has a reversible action available
- **WHEN** a user sends `/undo` and their most recent reversible history entry has a non-null `revert_payload`
- **THEN** the system applies the inverse operation, marks the entry as reverted, and replies with a localized confirmation (e.g., "undo.success" key)

#### Scenario: User has no reversible action
- **WHEN** a user sends `/undo` and no unreleased reversible history entry exists for that user in that chat
- **THEN** the system replies with a localized "nothing to undo" message (e.g., "undo.nothing" key) and takes no further action

#### Scenario: Revert payload deserialization fails
- **WHEN** `UndoService` cannot deserialize the `revert_payload` of the latest entry (e.g., stale schema)
- **THEN** the system logs a Warning, replies with a localized "cannot undo this action" message (e.g., "undo.error" key), and does NOT mark the entry as reverted

#### Scenario: /undo works in both group and private chats
- **WHEN** a user sends `/undo` in a group chat or a private chat
- **THEN** the system processes the undo scoped to that chat + user combination in both cases

---

### Requirement: UndoService orchestrates inverse operations per action type
The system SHALL implement an `UndoService` that, given a history entry, dispatches the correct inverse repository operation based on `action_type`. Supported action types for undo SHALL include at minimum: `ItemBought` (re-add item to list as not bought), `ItemRemoved` (restore item), `ListArchived` (unarchive list). Unsupported action types SHALL result in the "undo.error" reply without modification.

#### Scenario: ItemBought action is undone
- **WHEN** the latest reversible entry has `action_type = ItemBought`
- **THEN** `UndoService` restores the item to its pre-bought state via the shopping list repository and replies with success

#### Scenario: ItemRemoved action is undone
- **WHEN** the latest reversible entry has `action_type = ItemRemoved`
- **THEN** `UndoService` re-inserts the item with its original name and shopping list association and replies with success

#### Scenario: Unsupported action type
- **WHEN** the latest reversible entry has an `action_type` that `UndoService` does not handle
- **THEN** `UndoService` logs a Warning and replies with "undo.error"

---

### Requirement: Reverted history entries are marked to prevent double-undo
The system SHALL add a nullable `reverted_at TEXT` column to `BotActionHistory`. When `UndoService` successfully applies an inverse operation, it SHALL update that row's `reverted_at` to the current UTC timestamp in ISO-8601 format. Entries with a non-null `reverted_at` SHALL be excluded from `GetLatestReversibleAsync` results.

#### Scenario: Entry is marked after successful undo
- **WHEN** `UndoService` successfully applies the inverse operation
- **THEN** the history row's `reverted_at` is set to the current UTC timestamp

#### Scenario: Already-reverted entry is not returned for undo
- **WHEN** the only history entry for a user+chat has `reverted_at IS NOT NULL`
- **THEN** `GetLatestReversibleAsync` returns null and `/undo` replies with "undo.nothing"
