## Why

This project establishes a new .NET 9 AOT-compiled binary that runs a Telegram bot, loading configuration from a `.env` file and providing structured logging. This is needed to build a lightweight, fast-starting bot service with minimal runtime overhead and no JIT dependency.

## What Changes

- New .NET 9 console application project with AOT compilation enabled
- Telegram bot integration via the `Telegram.Bot` library (AOT-compatible configuration)
- `.env` file loading for configuration (bot token, environment settings)
- Structured logging via `Microsoft.Extensions.Logging` with console output
- Docker-compatible single-file AOT binary output

## Capabilities

### New Capabilities

- `telegram-bot-core`: Core Telegram bot setup — polling loop, update handler wiring, bot client initialization from config
- `dotenv-config`: Load configuration from a `.env` file at startup; map values into `IConfiguration`
- `structured-logging`: Application-wide structured logging setup with configurable log levels via environment/`.env`

### Modified Capabilities

*(none — this is a new project)*

## Impact

- **New project**: standalone console app, no existing codebase affected
- **Dependencies added**: `Telegram.Bot`, `DotNetEnv` (or equivalent dotenv loader), `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Hosting`
- **AOT constraints**: reflection must be avoided; source generators used where needed; trimming annotations required for Telegram.Bot JSON serialization
- **Rollback plan**: change is entirely additive (new project directory); revert by deleting the project folder — no shared infrastructure affected
- **Affected teams**: sole developer; no cross-team impact
