# Household Planner

A .NET 9 AOT-compiled Telegram bot for household shopping lists and meal planning. Built with native AOT for minimal Docker image size and fast startup on a low-cost VPS.

## What this is

A Telegram bot that helps families and households manage shopping lists and meal planning with multi-language support.

### Current features

- **Shopping list management** (`/buy`, `/list`) — Add items, paginate, mark as bought
- **Meal planning** (`/meals`) — Create, view, and manage meals with ingredients and steps
- **Purchase history** — Track what was bought and where
- **Price capture** — Record prices when items are purchased
- **Multi-language support** — English, Russian, and Polish with per-chat language preferences
- **History tracking** — Record all user actions (items added, bought, removed)

Infrastructure includes bot polling, full dependency injection, structured JSON logging, .env config, Docker AOT publishing, and comprehensive test coverage.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- A Telegram bot token from [@BotFather](https://t.me/BotFather)
- Docker (optional, for containerized deployment)
- For AOT publishing: `clang` and `zlib1g-dev` (Linux)

## Getting started

### 1. Clone and configure

```bash
cd ProductTrackerBot
cp .env.example .env
```

Edit `.env` and set your bot token:

```env
BOT_TOKEN=123456789:AABBccDDeeffGGHHiijjKKllmmNNoopp
```

Optional log level override (default is `Information`):

```env
Logging__LogLevel__Default=Debug
```

### 2. Run in development

```bash
dotnet run --project ProductTrackerBot
```

The bot starts polling immediately. Send any message to it in Telegram — you will see structured JSON log output for each received update.

### 3. Build and run the AOT binary

```bash
dotnet publish ProductTrackerBot -c Release -r linux-x64 /p:PublishAot=true
./ProductTrackerBot/bin/Release/net9.0/linux-x64/publish/ProductTrackerBot
```

### 4. Docker Compose (recommended for deployment)

```bash
make up        # Build image and start in the background
make restart   # Rebuild image and recreate container (use after code changes)
make down      # Stop and remove containers
make logs      # Tail container logs
```

`make restart` is the correct command when you want to deploy code changes — it rebuilds the Docker image and force-recreates the container. `make up` starts without rebuilding if the image already exists.

> **Note:** `make run` runs the bot locally via `dotnet run`, outside Docker. Do not use it to restart the container — it will conflict with a running container instance (Telegram 409 error).

The Dockerfile uses a multi-stage build that produces a self-contained AOT binary in a minimal `runtime-deps` image — no .NET runtime required at runtime.

## Configuration

All configuration is via environment variables (or `.env` file loaded at startup).

| Variable | Required | Default | Description |
|---|---|---|---|
| `BOT_TOKEN` | Yes | — | Telegram bot token from BotFather |
| `Logging__LogLevel__Default` | No | `Information` | Log verbosity (`Trace`, `Debug`, `Information`, `Warning`, `Error`) |

`.env` file takes lowest precedence — real environment variables always win.

## Project structure

```
ProductTrackerBot/
├── Program.cs                  # Entry point: DI wiring, host startup
├── BotConfiguration.cs         # Typed config record (Token)
├── BotHostedService.cs         # Background service: starts/stops long-polling
├── UpdateDispatcher.cs         # Routes updates to handlers
├── UpdateHandler.cs            # Telegram update handling interface
├── Handlers/                   # Command, callback, and dialog handlers
├── Repositories/               # Data access (SQLite)
├── Services/                   # Business logic (ShoppingListService, MealMergeService)
├── Localization/               # Multi-language support (en, ru, pl)
├── Models/                     # Data models and payloads
├── Database/                   # Schema initialization
├── Dockerfile                  # Multi-stage AOT build
├── .env.example                # Config template
└── TrimmerRoots.xml            # AOT trimming hints
```

## Tech stack

| Concern | Package |
|---|---|
| Telegram client | `Telegram.Bot` v22.9.6.2 (AOT-compatible) |
| .env loading | `DotNetEnv` v3.0.0 |
| DI / hosting | `Microsoft.Extensions.Hosting` v9.0 |
| Logging | `Microsoft.Extensions.Logging.Console` v9.0 (JSON) |
| Code quality | `StyleCop.Analyzers` |

## AOT notes

AOT compilation is enabled with `PublishAot=true`. Constraints that must be respected when adding features:

- No runtime reflection or `dynamic`
- No `Assembly.Load` / `Activator.CreateInstance` with unknown types
- Use constructor injection only (no property injection)
- Serialization uses `System.Text.Json` source generators

## Logging

Logs are emitted as JSON to stdout. Example:

```json
{"Timestamp":"2026-05-07T10:00:00.000Z","EventId":0,"LogLevel":"Information","Category":"ProductTrackerBot.BotHostedService","Message":"Bot started polling"}
```

Credentials (bot token) are never logged.

## Testing

```bash
dotnet test
```

Comprehensive test suite covers handlers, services, repositories, and localization with proper mocking.
