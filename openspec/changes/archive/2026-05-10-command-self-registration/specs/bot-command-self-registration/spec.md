## ADDED Requirements

### Requirement: Command handlers expose optional description metadata
Each `ICommandHandler` implementation SHALL expose a nullable `string? Description` property. Handlers returning a non-null description are considered "public" commands visible in Telegram's suggestion list and in the `/start` reply.

#### Scenario: Handler with description
- **WHEN** an `ICommandHandler` returns a non-null, non-empty `Description`
- **THEN** that command SHALL be included in the `setMyCommands` payload and in the `/start` reply list

#### Scenario: Handler without description
- **WHEN** an `ICommandHandler` returns `null` from `Description`
- **THEN** that command SHALL be excluded from both `setMyCommands` and the `/start` reply list

---

### Requirement: Bot registers commands with Telegram at startup
The system SHALL call `SetMyCommandsAsync` once during application startup, passing the list of all commands whose `Description` is non-null.

#### Scenario: Successful registration
- **WHEN** the application starts and at least one handler has a non-null `Description`
- **THEN** `SetMyCommandsAsync` is called with those commands and Telegram displays them in the `/` suggestion list

#### Scenario: Registration failure at startup
- **WHEN** `SetMyCommandsAsync` throws an exception
- **THEN** the error SHALL be logged at Warning level and the polling loop SHALL start regardless

#### Scenario: No commands with descriptions
- **WHEN** no registered `ICommandHandler` has a non-null `Description`
- **THEN** `SetMyCommandsAsync` SHALL NOT be called and startup proceeds normally

---

### Requirement: /start command replies with available command list
The system SHALL handle the `/start` command by sending a localized welcome message followed by a formatted list of all commands that have a non-null `Description`.

#### Scenario: User sends /start with commands available
- **WHEN** a user sends `/start` and at least one handler has a non-null `Description`
- **THEN** the bot SHALL reply with a welcome message and a list of `/<command> — <description>` entries

#### Scenario: User sends /start with no public commands
- **WHEN** a user sends `/start` and no handler has a non-null `Description`
- **THEN** the bot SHALL reply with a localized fallback message indicating no commands are available

#### Scenario: /start works in both private and group chats
- **WHEN** `/start` is sent in either a private chat or a group chat
- **THEN** the bot SHALL reply with the command list (no group-only restriction applies)
