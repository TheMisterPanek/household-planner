## ADDED Requirements

### Requirement: Load .env file into configuration before host builds
The system SHALL read a `.env` file from the working directory and populate environment variables from it before the `IConfiguration` provider chain is constructed.

#### Scenario: .env file present
- **WHEN** a `.env` file exists in the working directory at startup
- **THEN** all key=value pairs are loaded into the process environment and accessible via `IConfiguration`

#### Scenario: .env file absent
- **WHEN** no `.env` file exists in the working directory
- **THEN** the application SHALL start successfully, relying on existing environment variables without throwing

#### Scenario: Duplicate key in .env and environment
- **WHEN** a key is defined both in `.env` and as a real environment variable
- **THEN** the real environment variable takes precedence (`.env` does not override existing env vars)

---

### Requirement: Bot token is required configuration
The system SHALL require `BOT_TOKEN` to be present in the resolved configuration (from `.env` or real environment).

#### Scenario: Token present
- **WHEN** `BOT_TOKEN` is set (via `.env` or environment)
- **THEN** the value is bound to `BotConfiguration.Token` and passed to the bot client

#### Scenario: Token absent
- **WHEN** `BOT_TOKEN` is not set in any configuration source
- **THEN** the application SHALL throw an `InvalidOperationException` with message "BOT_TOKEN is required" at startup
