## MODIFIED Requirements

### Requirement: /start handler sends welcome message and registers bot commands
The system SHALL, upon receiving `/start`, send a localized welcome message listing public commands AND call `SetMyCommandsAsync` to register those commands with the Telegram Bot API, enabling the "/" autocomplete menu in Telegram clients.

#### Scenario: /start with public commands available
- **WHEN** a user sends `/start` and at least one `ICommandHandler` has a non-null `Description`
- **THEN** the system SHALL send the localized welcome message listing those commands AND call `SetMyCommandsAsync` with the matching `BotCommand` list

#### Scenario: /start with no public commands
- **WHEN** a user sends `/start` and no `ICommandHandler` has a non-null `Description`
- **THEN** the system SHALL send the localized `start.no-commands` message and SHALL NOT call `SetMyCommandsAsync`

#### Scenario: Command registration failure does not affect welcome message
- **WHEN** `SetMyCommandsAsync` raises an exception during `/start`
- **THEN** the welcome message SHALL already have been delivered and the system SHALL log a Warning without surfacing an error to the user
