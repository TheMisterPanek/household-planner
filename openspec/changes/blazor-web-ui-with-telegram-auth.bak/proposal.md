## Why

The bot covers household inventory well but everything lives inside a chat window — no overview dashboard, no bulk editing, no search-while-you-type, no visual layout. A web UI lets household members manage the full inventory without learning bot commands, review history at a glance, and perform CRUD operations more comfortably than inline keyboards allow.

Authentication must stay frictionless: the household already trusts Telegram, so re-using Telegram as the identity provider (code-on-chat flow, analogous to Steam Guard) means no new passwords, no OAuth dance with a third-party, and no PII stored beyond what the bot already holds.

## What Changes

### New project: `ProductTrackerBot.Web` (Blazor Server)

A new `.csproj` added to the solution. It shares the same SQLite database file and the existing repository layer via project reference to `ProductTrackerBot`.

### Authentication: Telegram login-code flow

1. Browser visits `/login` → server generates a one-time 6-digit code, stores it in memory with a 5-minute TTL, and shows it on screen.
2. Page instructs the user to send `/login <code>` to the bot.
3. `/login` command handler verifies the code, marks the session as authenticated (stores `chatId` in an in-memory session store, keyed by a secure random token), and replies "Authenticated. You can close that tab."
4. Browser polls `/api/auth/status?token=<token>` every 2 s; when the session is marked valid it redirects to the dashboard.
5. Browser stores the token in a secure `HttpOnly` cookie; subsequent requests are validated server-side.

No email, no password. Token TTL: 24 h (configurable via env var `WEB_SESSION_TTL_HOURS`).

### Web features (full CRUD mirror of bot capabilities)

| Feature | Pages / Components |
|---|---|
| Shopping list | `/list` — view, add item, mark done, delete, edit name/qty/expiry |
| Purchase history | `/history` — filterable table with pagination |
| Price log | `/prices` — per-item price history with sparkline |
| Meals library | `/meals` — add/edit/delete meals; view ingredients & steps |
| Weekly meal plan | `/week` — drag-assign meals to Mon–Sun |
| Settings | `/settings` — language preference, expiry notification threshold |
| AI query | `/ai` — chat interface backed by the existing `AiQueryService` |

### UI design: minimalistic & modern

- **Framework**: Blazor Server (.NET 9), Tailwind CSS via CDN, no JS framework
- **Layout**: White background, single sidebar nav, content area with 8-column max-width grid
- **Color palette**: Neutral grays + one accent (`indigo-600`); no decorative icons unless functional
- **Typography**: System font stack (`ui-sans-serif`), 14 px base
- **Cards**: Rounded-lg, subtle shadow (`shadow-sm`), no border by default
- **Tables**: Striped rows, sticky header, inline edit-in-place for simple fields
- **Forms**: Floating labels, inline validation, single-action buttons (no modal dialogs for simple edits)
- **Dark mode**: Not in scope for this change

### No migration to Blazor for bot

The Telegram bot continues to run unchanged alongside the web. The web UI is additive. Both share the same `ProductTrackerBot` class library (repositories, services, models).

## Capabilities

### New Capabilities

- `web-auth-login`: Household member generates a login code on the site, sends `/login <code>` to the bot, and is redirected to the dashboard.
- `web-shopping-list`: Full CRUD for shopping items from a browser.
- `web-purchase-history`: Browse and filter purchase history.
- `web-price-log`: View price history per item.
- `web-meals`: Add, edit, delete meals (with ingredients and steps).
- `web-week-plan`: Assign meals to days of the week visually.
- `web-settings`: Change language and notification threshold without bot commands.
- `web-ai`: Send natural-language inventory queries from a web chat box.

## Out of Scope

- Mobile-responsive PWA / native app.
- Multi-user / multi-household access control (single household per DB assumed).
- Real-time push for list changes (no SignalR in this iteration).
- Dark mode.
- Moving bot logic out of `ProductTrackerBot` — the bot keeps running.

## Impact

- **Code**: 1 new project (`ProductTrackerBot.Web`); 1 new handler (`LoginCommandHandler`) in `ProductTrackerBot`; shared repositories and services referenced via project ref.
- **APIs**: New HTTP endpoints under `/api/auth/*` (internal polling only).
- **Dependencies**: `Microsoft.AspNetCore.Components.Web` (already in .NET 9 SDK); Tailwind CSS via CDN script tag; no new NuGet packages beyond what Blazor Server requires.
- **Systems**: Same SQLite file. Bot process and web process must share the DB file — run in the same Docker container or mount the same volume.
- **Compatibility**: Blazor Server is reflection-heavy; AOT is not applicable to the web project. `ProductTrackerBot` (the class library) must remain AOT-safe; the web project does not need to be.

## Rollback Plan

1. Remove `ProductTrackerBot.Web` from the solution (`dotnet sln remove`).
2. Remove `LoginCommandHandler.cs` from `ProductTrackerBot/Handlers/`.
3. Remove the `AddScoped<ICommandHandler, LoginCommandHandler>()` line from `Program.cs`.
4. Remove `/login` localization keys from `Strings.*.json`.
5. Bot continues working without changes; web container can simply be stopped.

## Affected Teams

Single-user/household deployment. No shared infrastructure impact beyond running a second HTTP port.

## Cross-Cutting Notes

- The web project references `ProductTrackerBot` as a project dependency; no circular references.
- `LoginCommandHandler` follows the existing `ICommandHandler` pattern; registers via `AddScoped`.
- Login code store is an `IHostedService`-adjacent singleton with background expiry sweep; no Hangfire.
- All bot-side user-facing text in `LoginCommandHandler` flows through `ILocalizer`.
- `IHistoryRepository.RecordAsync` is not called for login events (authentication is not an inventory mutation).
- The web session store is an in-memory `ConcurrentDictionary`; restart clears all sessions (acceptable for household use — users re-login after deploy).
- Shared SQLite file: only one writer at a time. Blazor Server and the bot share the same process in the reference deployment (Docker Compose single container); if split, WAL mode must be enabled.
