## MODIFIED Requirements

### Requirement: Bot starts polling on application startup
The system SHALL initialize a `TelegramBotClient` using the bot token from configuration and begin long-polling for updates when the hosted application starts. The SQLite connection string SHALL be resolved from the `DB_PATH` environment variable when present, falling back to the `AppContext.BaseDirectory`-relative default path.

#### Scenario: Successful startup
- **WHEN** the application starts with a valid `BOT_TOKEN` in configuration
- **THEN** the polling loop begins and the bot logs "Bot polling started" at Information level

#### Scenario: Missing token at startup
- **WHEN** the application starts without `BOT_TOKEN` set
- **THEN** the application SHALL fail fast with a clear error message before entering the polling loop

#### Scenario: Startup with DB_PATH configured
- **WHEN** the application starts with `DB_PATH=/data/product-tracker.db`
- **THEN** the SQLite connection uses the specified path and `DatabaseInitializer` runs against that file
