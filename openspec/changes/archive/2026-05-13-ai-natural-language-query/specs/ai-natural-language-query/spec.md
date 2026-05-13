## ADDED Requirements

### Requirement: User can query shopping data via /ai command
The system SHALL accept a natural language question following the `/ai` command and return a natural language answer derived from the current chat's SQLite data. The bot SHALL resolve the current `GroupId` for the chat before processing. If the question text is empty, the bot SHALL reply with a localized usage hint.

#### Scenario: Successful question answered
- **WHEN** a user sends `/ai what did we buy most this month?` in a group chat
- **THEN** the bot resolves the GroupId, generates a SQL query via Round 1 AI call, executes it read-only, sends results to Round 2 AI call, and replies with a formatted natural language answer

#### Scenario: Empty question text
- **WHEN** a user sends `/ai` with no question text
- **THEN** the bot replies with a localized usage hint (e.g., "Usage: /ai <your question>") and does not call the AI

#### Scenario: Command in private chat
- **WHEN** a user sends `/ai <question>` in a private chat (DM with bot)
- **THEN** the bot resolves the ChatId-based context and responds normally (private chats are supported)

---

### Requirement: User can query via bot mention
The system SHALL treat a message mentioning the bot (`@BotName <question>`) as equivalent to `/ai <question>`. Detection SHALL use Telegram message entities of type `Mention`, not text pattern matching. The bot's own username SHALL be resolved once at startup via `GetMeAsync()` and compared case-insensitively to entity text.

#### Scenario: Mention at start of message
- **WHEN** a user sends `@BotName what's on the shopping list?`
- **THEN** the bot strips `@BotName` from the text, trims whitespace, and processes the remaining text as the question

#### Scenario: Mention mid-sentence
- **WHEN** a user sends `hey @BotName how much did we spend last week?`
- **THEN** the bot strips `@BotName`, trims, and processes the resulting question

#### Scenario: Mention with no question text
- **WHEN** a user sends only `@BotName` with no additional text
- **THEN** the bot replies with the same localized usage hint as an empty `/ai` command

#### Scenario: Message mentions another user (not the bot)
- **WHEN** a message contains `@OtherUser some text` with no mention of the bot
- **THEN** the bot does NOT trigger AI query handling; the message falls through to normal dialog handling

---

### Requirement: AI generates scoped SQL using parameterized placeholders
The system SHALL instruct the AI (via IDENTITY.md system prompt) to write SELECT queries using `@groupId` and `@chatId` as named placeholders for the current chat's scope. The bot SHALL validate the returned SQL contains at least one of these tokens before execution. Queries failing validation SHALL be rejected without execution and the user SHALL receive a localized error message.

#### Scenario: Valid scoped query executed
- **WHEN** the AI returns `SELECT ItemName, COUNT(*) FROM PurchaseHistory WHERE GroupId = @groupId GROUP BY ItemName ORDER BY 2 DESC LIMIT 10`
- **THEN** the bot executes the query with `@groupId` bound to the resolved GroupId and returns results to Round 2

#### Scenario: AI omits required placeholder
- **WHEN** the AI returns a SQL query with no `@groupId` or `@chatId` token
- **THEN** the bot rejects the query without executing it, logs a Warning, and replies with a localized error (`ai.error.unsafe-query`)

#### Scenario: AI returns non-SELECT SQL
- **WHEN** the AI returns an INSERT, UPDATE, DELETE, or DROP statement
- **THEN** `PRAGMA query_only = ON` causes SQLite to reject the statement; the bot catches the exception, passes an empty result to Round 2, and the AI responds gracefully

---

### Requirement: SQL is executed on a read-only connection
The system SHALL open a new SQLite connection per query and set `PRAGMA query_only = ON` before executing any AI-generated SQL. Result rows SHALL be limited to 50 (enforced by the bot appending `LIMIT 50` if the query does not already include one). Formatted results exceeding 3000 characters SHALL be truncated before being sent to Round 2.

#### Scenario: Read-only enforcement blocks write SQL
- **WHEN** any SQL statement that would modify data is executed on the query connection
- **THEN** SQLite rejects the statement with an error; the bot catches it and does not expose the raw error to the user

#### Scenario: Result row cap applied
- **WHEN** the AI-generated query would return more than 50 rows
- **THEN** the result set is capped at 50 rows; the Round 2 AI call receives the capped result

#### Scenario: Empty result set
- **WHEN** the SQL query executes successfully but returns zero rows
- **THEN** the bot passes the empty result to Round 2; the AI replies with a natural language message indicating no data was found (e.g., "No purchases found for this period")

#### Scenario: SQL syntax error
- **WHEN** the AI returns syntactically invalid SQL that fails to execute
- **THEN** the bot catches `SqliteException`, passes an error note to Round 2, and the AI replies gracefully without exposing raw SQL errors

---

### Requirement: AI service errors are handled gracefully
The system SHALL catch OpenRouter HTTP errors (timeouts, 5xx, 429 rate-limits) without throwing from the handler. On failure, the bot SHALL reply with a localized error message and log at Warning level.

#### Scenario: OpenRouter unavailable
- **WHEN** the OpenRouter API returns a 5xx error or times out (> 30s)
- **THEN** the bot replies with `ai.error.service-unavailable` and logs at Warning with the HTTP status

#### Scenario: Round 1 fails (SQL not generated)
- **WHEN** Round 1 AI call fails or returns no SQL
- **THEN** the bot does NOT proceed to execution or Round 2; it replies with a localized error immediately

#### Scenario: Round 2 fails (formatting not generated)
- **WHEN** Round 2 AI call fails after successful SQL execution
- **THEN** the bot replies with a localized fallback message; raw query results are NOT sent directly to the user
