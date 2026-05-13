## MODIFIED Requirements

### Requirement: Update dispatcher routes incoming updates to a handler
The system SHALL pass each received `Update` object to an `UpdateDispatcher` that resolves the correct `ICommandHandler` by command text or `ICallbackHandler` by callback data prefix. For non-command text messages, the dispatcher SHALL first check for a bot mention entity before falling through to dialog handler resolution. The bot's own username SHALL be resolved once at startup via `GetMeAsync()` and injected into the dispatcher.

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

#### Scenario: Non-command message mentions the bot
- **WHEN** a text message does not start with `/` and contains a `Mention` entity matching the bot's username
- **THEN** the dispatcher SHALL extract the question (text with `@BotName` stripped and trimmed) and invoke the AI query handler; dialog handler resolution SHALL NOT run for this message

#### Scenario: Non-command message does not mention the bot
- **WHEN** a text message does not start with `/` and contains no `Mention` entity matching the bot's username
- **THEN** the dispatcher SHALL fall through to `TryHandleDialogMessageAsync` as before
