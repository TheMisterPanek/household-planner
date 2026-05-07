## ADDED Requirements

### Requirement: Bot starts polling on application startup
The system SHALL initialize a `TelegramBotClient` using the bot token from configuration and begin long-polling for updates when the hosted application starts.

#### Scenario: Successful startup
- **WHEN** the application starts with a valid `BOT_TOKEN` in configuration
- **THEN** the polling loop begins and the bot logs "Bot polling started" at Information level

#### Scenario: Missing token at startup
- **WHEN** the application starts without `BOT_TOKEN` set
- **THEN** the application SHALL fail fast with a clear error message before entering the polling loop

---

### Requirement: Update dispatcher routes incoming updates to a handler
The system SHALL pass each received `Update` object to an `UpdateDispatcher` that resolves the correct `ICommandHandler` by command text or `ICallbackHandler` by callback data prefix.

#### Scenario: Message update with known command
- **WHEN** a Telegram user sends a text message starting with a known command (e.g. `/buy`, `/list`)
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

---

### Requirement: Graceful shutdown on cancellation
The system SHALL stop polling and dispose the bot client when the application lifetime is cancelled.

#### Scenario: Application receives SIGTERM
- **WHEN** the host receives a stop signal (SIGTERM / Ctrl+C)
- **THEN** the polling loop exits cleanly and logs "Bot polling stopped" at Information level before the process exits
