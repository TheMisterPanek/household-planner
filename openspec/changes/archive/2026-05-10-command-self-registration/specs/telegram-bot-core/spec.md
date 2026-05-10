## MODIFIED Requirements

### Requirement: Update dispatcher routes incoming updates to a handler
The system SHALL pass each received `Update` object to an `UpdateDispatcher` that resolves the correct `ICommandHandler` by command text or `ICallbackHandler` by callback data prefix. The `/start` command SHALL be treated as a valid routable command (mapped to `StartCommandHandler`) and SHALL NOT fall through to the unknown-command log path.

#### Scenario: Message update with known command
- **WHEN** a Telegram user sends a text message starting with a known command (e.g. `/buy`, `/list`, `/start`)
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

## ADDED Requirements

### Requirement: Bot registers commands with Telegram before polling starts
The system SHALL call `SetMyCommandsAsync` during the startup phase, before the polling loop begins accepting updates. This step is performed by a dedicated `IHostedService` registered ahead of `BotHostedService`.

#### Scenario: Command registration precedes polling
- **WHEN** the application starts
- **THEN** `BotCommandRegistrationService.StartAsync` completes before `BotHostedService.ExecuteAsync` begins long-polling
