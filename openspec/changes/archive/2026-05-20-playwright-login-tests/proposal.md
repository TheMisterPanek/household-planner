## Why

The login screen (`/login`) is a critical user-facing page that has no automated UI tests. Verifying it manually after changes is error-prone; Playwright tests will catch regressions in the login code display, countdown timer, expiry/refresh flow, and redirect behavior without requiring a real Telegram bot interaction.

## What Changes

- Add a new `ProductTrackerBot.Web.Tests.E2E` project with Playwright and NUnit
- Add Playwright tests covering the login page's core behaviors:
  - Page loads and displays a 6-character login code
  - Countdown timer decrements and shows "Code expires in N s"
  - When countdown reaches 0, "Get new code" button replaces the timer
  - Clicking "Get new code" resets the countdown and generates a new code
- Wire the new test project into the solution and Makefile

## Capabilities

### New Capabilities

- `playwright-login-screen`: End-to-end tests for the `/login` page verifying code display, countdown, expiry state, and code refresh behavior

### Modified Capabilities

<!-- none -->

## Impact

- New project `ProductTrackerBot.Web.Tests.E2E` (NUnit + Playwright) added to the solution
- No changes to production code
- `make test` or a new `make test-e2e` target runs the Playwright suite
- Playwright browsers must be installed on the CI/dev machine (`playwright install`)
- Rollback: delete the new project; no production code is touched
