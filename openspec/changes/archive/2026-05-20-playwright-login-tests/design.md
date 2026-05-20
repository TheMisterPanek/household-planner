## Context

The web app (`ProductTrackerBot.Web`) serves a Blazor Server login page at `/login`. It currently has no automated UI tests. The page generates a short-lived code, counts down 300 s, and polls for bot confirmation.

## Goals / Non-Goals

**Goals:**
- Set up a Playwright + NUnit E2E test project targeting the running web app
- Cover the login page's observable behaviors: code display, countdown, expiry, and refresh
- Tests run against a locally started app with a stub `LoginCodeStore` to remove Telegram dependency

**Non-Goals:**
- Testing the actual Telegram bot handshake (requires a live bot token)
- Testing the Dashboard or any post-login pages in this change
- CI pipeline integration (done separately)

## Decisions

### Test project: NUnit + Microsoft.Playwright

**Why NUnit over xUnit**: Playwright's official .NET SDK ships a `PageTest` base class that integrates with NUnit. Using xUnit requires more manual fixture wiring. NUnit is the path of least resistance for Playwright in .NET.

**Why a separate project**: Playwright tests start a real process and need a running HTTP server. Mixing them into `ProductTrackerBot.Tests` (xUnit, in-memory) would conflict with the test runner and make dependency management harder.

### Application startup in tests

Tests use `WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing` to spin up the web app in-process on a random port. Playwright's browser then hits that port via `http://localhost:<port>`.

To avoid needing a real Telegram bot token, the factory overrides `BotConfiguration` with a dummy token and replaces `LoginCodeStore` with a test double that returns predictable codes and never polls Telegram.

### Countdown and expiry

The countdown takes 300 s in production. Tests do not wait 300 s. Instead, the test double's `GenerateCode` returns a code whose expiry is set to 1 s in the future, letting the expiry path be exercised in under 3 s.

## Risks / Trade-offs

- [Blazor Server SignalR] → Playwright must wait for the Blazor circuit to connect before asserting interactive state. Mitigate: use `WaitForSelectorAsync` with visible state rather than fixed delays.
- [BOT_TOKEN required at startup] → `WebApplicationFactory` configures a placeholder token; `TelegramBotClient` will never be called during tests. The `BotHostedService` is removed from the test host to prevent failed connections.
- [Flakiness from timers] → The test double exposes a method to force expiry synchronously, avoiding real-time waits beyond 1-2 s.
