## ADDED Requirements

### Requirement: Application logs to stdout in JSON format
The system SHALL emit all log entries as JSON objects to stdout, one entry per line, using `Microsoft.Extensions.Logging` with the JSON console formatter.

#### Scenario: Log entry emitted
- **WHEN** any component calls `ILogger.Log*` at or above the configured minimum level
- **THEN** a JSON object is written to stdout containing at minimum: `Timestamp`, `Level`, `Message`, `Category`

#### Scenario: Log output is machine-parseable
- **WHEN** the application runs in production
- **THEN** each stdout line SHALL be valid JSON parseable by log aggregators (e.g., Loki, Datadog)

---

### Requirement: Log level is configurable via environment variable
The system SHALL read the minimum log level from the `Logging__LogLevel__Default` environment variable (standard .NET hierarchical env override).

#### Scenario: Custom log level set
- **WHEN** `Logging__LogLevel__Default=Debug` is set in `.env` or environment
- **THEN** Debug-level and above entries are emitted

#### Scenario: No log level configured
- **WHEN** `Logging__LogLevel__Default` is not set
- **THEN** the default log level SHALL be `Information`

---

### Requirement: Sensitive data is not logged
The system SHALL never log the bot token or any credential values, even at Debug level.

#### Scenario: Startup configuration logged
- **WHEN** the application logs its configuration at startup
- **THEN** `BOT_TOKEN` and any secret fields are masked or omitted from the log output
