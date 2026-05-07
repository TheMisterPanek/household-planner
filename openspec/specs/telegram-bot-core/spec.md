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
The system SHALL pass each received `Update` object to a registered `IUpdateHandler` implementation.

#### Scenario: Message update received
- **WHEN** a Telegram user sends a text message to the bot
- **THEN** the update is dispatched to the handler within the same polling iteration

#### Scenario: Unhandled update type
- **WHEN** an update type has no registered handler logic
- **THEN** the system SHALL log a Debug-level message and continue polling without throwing

---

### Requirement: Graceful shutdown on cancellation
The system SHALL stop polling and dispose the bot client when the application lifetime is cancelled.

#### Scenario: Application receives SIGTERM
- **WHEN** the host receives a stop signal (SIGTERM / Ctrl+C)
- **THEN** the polling loop exits cleanly and logs "Bot polling stopped" at Information level before the process exits
