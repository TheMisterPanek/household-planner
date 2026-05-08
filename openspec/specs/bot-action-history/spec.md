### Requirement: BotActionHistory table persists action records
The system SHALL maintain a `BotActionHistory` SQLite table that stores every significant bot action with timestamp, chat ID, user ID, user name, action type, and a JSON payload. The table SHALL be created via `CREATE TABLE IF NOT EXISTS` in `DatabaseInitializer` so it is safe to apply against an existing database.

#### Scenario: Table created on first startup
- **WHEN** the application starts against a database that has no `BotActionHistory` table
- **THEN** the table is created and subsequent writes succeed without error

#### Scenario: Table creation is idempotent
- **WHEN** the application restarts against a database that already has a `BotActionHistory` table
- **THEN** no error is raised and existing rows are preserved

---

### Requirement: IHistoryRepository appends and queries action entries
The system SHALL expose an `IHistoryRepository` interface with two operations:
- `RecordAsync(long chatId, long userId, string userName, BotActionType actionType, string payloadJson, CancellationToken ct)` — inserts a new row
- `GetRecentAsync(long chatId, int limit, CancellationToken ct)` — returns the most recent rows for the given chat in descending timestamp order

#### Scenario: RecordAsync inserts a row
- **WHEN** `RecordAsync` is called with valid chat ID, user ID, user name, action type, and payload
- **THEN** a row is inserted into `BotActionHistory` with a server-generated UTC timestamp and the provided values

#### Scenario: GetRecentAsync returns entries in descending order
- **WHEN** multiple actions have been recorded for a chat and `GetRecentAsync` is called with `limit = 10`
- **THEN** up to 10 entries are returned, most recent first, scoped to that chat ID

#### Scenario: GetRecentAsync returns empty list for unknown chat
- **WHEN** `GetRecentAsync` is called for a chat ID with no recorded actions
- **THEN** an empty list is returned without error

---

### Requirement: BotActionType enum identifies action kinds
The system SHALL define a `BotActionType` enum with values: `ItemAdded`, `ItemBought`, `ItemRemoved`, `ListViewed`, `HistoryViewed`. Values SHALL be stored as their string names in the `Payload` column context and as a separate `ActionType TEXT NOT NULL` column.

#### Scenario: All action types are representable
- **WHEN** each `BotActionType` variant is passed to `RecordAsync`
- **THEN** the stored `ActionType` text matches the enum member name exactly

---

### Requirement: History payload is AOT-safe JSON
The system SHALL serialize action payloads using a `[JsonSerializable]`-annotated source-generated `JsonSerializerContext` so that no runtime reflection is required.

#### Scenario: Serialization succeeds without reflection
- **WHEN** a payload object is serialized for storage
- **THEN** the result is valid JSON and no `InvalidOperationException` or trimming warning is produced during AOT compilation

---

### Requirement: /history command displays recent chat actions
The system SHALL register a `HistoryCommandHandler` for the `/history` command that, when invoked in a group chat, fetches the last 10 `BotActionHistory` entries for that chat and replies with a formatted text message.

#### Scenario: Chat has recorded actions
- **WHEN** a group member sends `/history` and at least one action has been recorded for that chat
- **THEN** the bot replies with a message listing up to 10 actions, each showing timestamp (UTC), user name, and action type

#### Scenario: Chat has no recorded actions
- **WHEN** a group member sends `/history` and no actions exist for that chat
- **THEN** the bot replies with "История пуста." (History is empty.)

#### Scenario: /history used in private chat
- **WHEN** a user sends `/history` in a private chat
- **THEN** the bot replies "Эта команда работает только в групповом чате." and takes no further action

---

### Requirement: Handler errors in history recording do not suppress bot responses
The system SHALL wrap every `IHistoryRepository.RecordAsync` call in a try/catch inside each handler. If `RecordAsync` throws, the exception SHALL be logged at Warning level and swallowed so the primary bot action (reply, edit, etc.) completes normally.

#### Scenario: RecordAsync throws an exception
- **WHEN** `RecordAsync` raises an exception (e.g., database locked)
- **THEN** the handler logs a Warning and the original Telegram reply is still sent to the user
