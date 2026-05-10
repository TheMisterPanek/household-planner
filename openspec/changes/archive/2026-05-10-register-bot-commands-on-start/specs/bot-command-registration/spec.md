## ADDED Requirements

### Requirement: Bot registers commands with Telegram on /start
The system SHALL call the Telegram Bot API `setMyCommands` endpoint with all registered `ICommandHandler` instances that expose a non-null `Description` when a user sends `/start`.

#### Scenario: Commands registered successfully
- **WHEN** a user sends `/start` and at least one `ICommandHandler` has a non-null `Description`
- **THEN** the system SHALL call `SetMyCommandsAsync` with a list of `BotCommand` entries matching those handlers (command text and description), in addition to sending the welcome message

#### Scenario: No public commands available
- **WHEN** a user sends `/start` and no `ICommandHandler` has a non-null `Description`
- **THEN** the system SHALL NOT call `SetMyCommandsAsync` and SHALL send the no-commands message instead

#### Scenario: Telegram API call fails during /start
- **WHEN** `SetMyCommandsAsync` throws an exception
- **THEN** the system SHALL log a Warning and the welcome message SHALL have already been delivered; the exception SHALL NOT propagate to the user
