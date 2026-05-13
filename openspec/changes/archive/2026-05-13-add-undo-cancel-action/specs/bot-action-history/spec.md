## MODIFIED Requirements

### Requirement: BotActionHistory table persists action records
The system SHALL maintain a `BotActionHistory` SQLite table that stores every significant bot action with timestamp, chat ID, user ID, user name, action type, a JSON payload, a nullable `revert_payload TEXT` column, and a nullable `reverted_at TEXT` column. The table SHALL be created via `CREATE TABLE IF NOT EXISTS` in `DatabaseInitializer` so it is safe to apply against an existing database. For databases where the table already exists, both new columns SHALL be added via `PRAGMA table_info` check + `ALTER TABLE ... ADD COLUMN`, ignoring `SqliteException` on duplicate-column errors.

#### Scenario: Table created on first startup
- **WHEN** the application starts against a database that has no `BotActionHistory` table
- **THEN** the table is created with all columns including `revert_payload` and `reverted_at`, and subsequent writes succeed without error

#### Scenario: Table creation is idempotent
- **WHEN** the application restarts against a database that already has a `BotActionHistory` table
- **THEN** no error is raised and existing rows are preserved

#### Scenario: Existing table gains new columns via migration
- **WHEN** the application starts against a database that has `BotActionHistory` but lacks `revert_payload` or `reverted_at`
- **THEN** both columns are added and existing rows have NULL for those columns

---

### Requirement: IHistoryRepository appends and queries action entries
The system SHALL expose an `IHistoryRepository` interface with the following operations:
- `RecordAsync(long chatId, long userId, string userName, BotActionType actionType, string payloadJson, string? revertPayloadJson, CancellationToken ct)` — inserts a new row; `revertPayloadJson` is nullable
- `GetRecentAsync(long chatId, int limit, CancellationToken ct)` — returns the most recent rows for the given chat in descending timestamp order
- `GetLatestReversibleAsync(long chatId, long userId, CancellationToken ct)` — returns the single most recent entry for that chat+user where `revert_payload IS NOT NULL` and `reverted_at IS NULL`; returns null if none exists
- `MarkRevertedAsync(long entryId, string revertedAt, CancellationToken ct)` — sets `reverted_at` on the given history row

#### Scenario: RecordAsync inserts a row with revert payload
- **WHEN** `RecordAsync` is called with a non-null `revertPayloadJson`
- **THEN** a row is inserted with the provided `revert_payload` value and `reverted_at = NULL`

#### Scenario: RecordAsync inserts a row without revert payload
- **WHEN** `RecordAsync` is called with `revertPayloadJson = null`
- **THEN** a row is inserted with `revert_payload = NULL` and `reverted_at = NULL`

#### Scenario: GetRecentAsync returns entries in descending order
- **WHEN** multiple actions have been recorded for a chat and `GetRecentAsync` is called with `limit = 10`
- **THEN** up to 10 entries are returned, most recent first, scoped to that chat ID

#### Scenario: GetRecentAsync returns empty list for unknown chat
- **WHEN** `GetRecentAsync` is called for a chat ID with no recorded actions
- **THEN** an empty list is returned without error

#### Scenario: GetLatestReversibleAsync returns the most recent undoable entry
- **WHEN** a user has recorded multiple actions and the most recent has a non-null `revert_payload` and null `reverted_at`
- **THEN** that entry is returned

#### Scenario: GetLatestReversibleAsync returns null when no reversible entry exists
- **WHEN** no entry for the chat+user has `revert_payload IS NOT NULL AND reverted_at IS NULL`
- **THEN** null is returned

#### Scenario: MarkRevertedAsync sets reverted_at
- **WHEN** `MarkRevertedAsync` is called with a valid entry ID and UTC timestamp
- **THEN** the row's `reverted_at` is set to the provided value
