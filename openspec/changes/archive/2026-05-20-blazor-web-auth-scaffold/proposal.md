## Why

Everything the bot can do today is locked behind a Telegram chat interface. A web UI unlocks comfortable CRUD editing, dashboard views, and searchable history — but none of that is shippable until there's a secure way to sign in from a browser without adding passwords or a third-party OAuth provider.

This change delivers the foundation: a new `ProductTrackerBot.Web` Blazor Server project wired to the existing SQLite database, a login-code flow (analogous to Steam Guard — request a code in the browser, send it to the bot, browser auto-redirects), and the shared layout shell that all future web pages will inherit.

## What Changes

### New project: `ProductTrackerBot.Web` (Blazor Server)

A new `.csproj` added to the solution. References `ProductTrackerBot` for repositories and services. Runs in the same process as the bot (single Docker container, shared DI root).

### Authentication: Telegram login-code flow

1. Browser visits `/login` → server generates a one-time 6-digit code + a 32-byte token, stores in memory with a 5-minute TTL, displays the code.
2. User sends `/login <code>` to the Telegram bot.
3. `LoginCommandHandler` calls `LoginCodeStore.TryVerify(code, chatId)` — marks the session valid, moves the entry to the token store.
4. Browser polls `/api/auth/status?token=<t>` every 2 s; on valid response, sets a signed `HttpOnly` cookie and redirects to `/`.
5. All subsequent Blazor pages read the `chatId` claim from the cookie.

Token TTL: 24 h (env var `WEB_SESSION_TTL_HOURS`). No email, no password.

### Shared layout shell

`MainLayout.razor` + `NavMenu.razor` provide the sidebar + content area that all future pages inherit. An `AuthenticatedPageBase` abstract component guards every page behind cookie auth.

## Capabilities

### New Capabilities

- `web-auth-login`: User generates a code on the site, sends `/login <code>` to the bot, browser redirects to the dashboard.
- `web-shell`: Navigable sidebar with placeholders for all future feature pages.

## Out of Scope

- Any feature pages (Shopping list, History, etc.) — delivered in `blazor-web-inventory-pages` and `blazor-web-advanced-pages`.
- Dark mode.
- Multi-household / multi-user access control.

## Impact

- **Code**: 1 new project (`ProductTrackerBot.Web`); 1 new handler + service in `ProductTrackerBot` (`LoginCommandHandler`, `LoginCodeStore`).
- **APIs**: `GET /api/auth/status` (internal polling only).
- **Dependencies**: No new NuGet packages beyond what Blazor Server requires (.NET 9 SDK); Tailwind CSS via CDN script tag.
- **Systems**: Same SQLite file. Bot and web share one process in the reference deployment.
- **Compatibility**: Web project is not AOT; `ProductTrackerBot` class library stays AOT-safe.

## Rollback Plan

1. Remove `ProductTrackerBot.Web` from the solution.
2. Remove `LoginCommandHandler.cs` and `LoginCodeStore.cs` from `ProductTrackerBot`.
3. Remove `AddScoped<ICommandHandler, LoginCommandHandler>()` and `AddSingleton<LoginCodeStore>()` from bot `Program.cs`.
4. Remove `login.*` localization keys from `Strings.*.json`.
5. Remove `ports: - "8080:8080"` from `docker-compose.yml`.

## Affected Teams

Single-user/household deployment. No shared infrastructure impact.

## Cross-Cutting Notes

- `LoginCommandHandler` follows the existing `ICommandHandler` pattern; registered via `AddScoped`.
- `LoginCodeStore` is `AddSingleton` — shared between bot and web in the same process host.
- All bot-side user-facing text in `LoginCommandHandler` flows through `ILocalizer`; no hardcoded strings.
- `IHistoryRepository.RecordAsync` is not called for login events (authentication is not an inventory mutation).
- Web session store uses an in-memory `ConcurrentDictionary`; restart clears sessions (acceptable for household use).
