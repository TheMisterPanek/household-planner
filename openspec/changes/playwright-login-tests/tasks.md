## 1. Project Setup

- [ ] 1.1 Create `ProductTrackerBot.Web.Tests.E2E` NUnit project with `Microsoft.Playwright` and `Microsoft.AspNetCore.Mvc.Testing` packages
- [ ] 1.2 Add the new project to `product-tracker-bot-2.sln`
- [ ] 1.3 Create a `LoginCodeStoreStub` test double that returns a fixed 6-character code and supports forcing instant expiry
- [ ] 1.4 Create a `LoginWebApplicationFactory` using `WebApplicationFactory<Program>` that replaces `LoginCodeStore` with the stub, sets a dummy `BOT_TOKEN` and `BOT_USERNAME=@TestBot`, and removes `BotHostedService` from hosted services

## 2. Playwright Test Infrastructure

- [ ] 2.1 Create `LoginPageTests` base fixture that starts the factory, obtains the base URL, and launches a Playwright browser page pointed at that URL
- [ ] 2.2 Verify `dotnet playwright install` instructions are captured in a `README.md` in the test project

## 3. Login Page Tests

- [ ] 3.1 Test: page title is "Sign in — Household Planner"
- [ ] 3.2 Test: login code is visible and matches `[A-Z0-9]{6}` after page load
- [ ] 3.3 Test: countdown text "Code expires in N s" is visible and "Get new code" button is absent on fresh load
- [ ] 3.4 Test: countdown decrements — after waiting 2 s the displayed number is lower than the initial value
- [ ] 3.5 Test: when stub forces expiry, "Get new code" button appears and countdown text is gone
- [ ] 3.6 Test: clicking "Get new code" regenerates the code and restores the countdown text
- [ ] 3.7 Test: bot username `@TestBot` appears in the instruction paragraph

## 4. Smoke Test

- [ ] 4.1 Run `dotnet test ProductTrackerBot.Web.Tests.E2E` and confirm all tests pass; manually navigate to `/login` in a browser against a locally running app to verify the page looks correct
