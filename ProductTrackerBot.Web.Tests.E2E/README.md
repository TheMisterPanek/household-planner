# ProductTrackerBot.Web.Tests.E2E

Playwright + NUnit end-to-end tests for the Household Planner web UI.

## Prerequisites

Install Playwright browsers once after building:

```bash
dotnet build ProductTrackerBot.Web.Tests.E2E
dotnet playwright install --with-deps chromium
```

> `dotnet playwright install` is a CLI tool shipped with `Microsoft.Playwright`.
> Run it from the repository root or the project directory.

## Running the tests

```bash
# From repo root:
make test-e2e

# Or directly:
dotnet test ProductTrackerBot.Web.Tests.E2E
```

The factory spins up the web app on a random port for each test run, so no
running server is needed.

## What's covered

| Test | Description |
|---|---|
| `PageTitle_IsSignIn` | `<title>` is "Sign in — Household Planner" |
| `LoginCode_MatchesPattern` | 6-char `[A-Z0-9]` code is visible after load |
| `FreshLoad_ShowsCountdown_AndHidesRefreshButton` | Countdown text visible; "Get new code" absent |
| `Countdown_DecrementsAfterTwoSeconds` | Timer decreases over 2 s |
| `ExpiredCode_ShowsRefreshButton_AndHidesCountdown` | Expiry state correct when stub forces 1 s TTL |
| `ClickingRefresh_RestoresCountdownAndHidesButton` | Refresh resets state |
| `BotUsername_AppearsInInstructions` | `@TestBot` from config is shown |
