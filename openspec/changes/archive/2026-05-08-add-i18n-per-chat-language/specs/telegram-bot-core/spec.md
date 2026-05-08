## MODIFIED Requirements

### Requirement: Update dispatcher routes incoming updates to a handler
The system SHALL pass each received `Update` object to an `UpdateDispatcher` that resolves the correct `ICommandHandler` by command text or `ICallbackHandler` by callback data prefix. The dispatcher SHALL register the `/language` command handler alongside all existing handlers.

#### Scenario: Message update with known command
- **WHEN** a Telegram user sends a text message starting with a known command (e.g. `/buy`, `/list`, `/language`)
- **THEN** the matching `ICommandHandler` is invoked for that update

#### Scenario: Callback query with known prefix
- **WHEN** a Telegram `CallbackQuery` arrives with data matching a registered `ICallbackHandler.CallbackPrefix`
- **THEN** the matching `ICallbackHandler` is invoked

#### Scenario: Message update with unknown command
- **WHEN** a text message starts with `/` but no `ICommandHandler` matches
- **THEN** the system SHALL log a Debug-level message and take no further action

#### Scenario: Unhandled update type
- **WHEN** an update type has no registered handler logic
- **THEN** the system SHALL log a Debug-level message and continue polling without throwing
