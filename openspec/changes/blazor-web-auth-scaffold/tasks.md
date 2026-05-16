## 1. Localization Keys

- [ ] 1.1 Add `login.usage`, `login.success`, `login.invalid` to `Strings.en.json`
- [ ] 1.2 Add same keys (Russian) to `Strings.ru.json`
- [ ] 1.3 Add same keys (Polish) to `Strings.pl.json`

---

## 2. `LoginCodeStore`

- [ ] 2.1 Create `ProductTrackerBot/Services/LoginCodeStore.cs`:
  - Constructor accepts `TimeProvider` for testability
  - `string GenerateCode(out string token)` — 6-digit zero-padded, 32-byte hex token; stores entry in `_codes` with 5-minute expiry via `CancellationTokenSource + Task.Delay`
  - `bool TryVerify(string code, long chatId)` — validates code + expiry; removes from `_codes`; writes to `_tokens` with 24 h TTL; returns false if missing/expired
  - `bool TryGetSession(string token, out long chatId)` — checks `_tokens`; validates TTL
  - `void Purge(string code)` — `_codes.TryRemove`
- [ ] 2.2 Register in `ProductTrackerBot/Program.cs`:
  ```csharp
  builder.Services.AddSingleton(TimeProvider.System);
  builder.Services.AddSingleton<LoginCodeStore>();
  ```

---

## 3. `LoginCommandHandler`

- [ ] 3.1 Create `ProductTrackerBot/Handlers/LoginCommandHandler.cs` implementing `ICommandHandler`:
  - `Command = "/login"`
  - Split `message.Text` on space; if no code part reply `login.usage` and return
  - `TryVerify(code.Trim(), chatId)` → success: `login.success`, failure: `login.invalid`
  - No `IHistoryRepository.RecordAsync` call
- [ ] 3.2 Register: `builder.Services.AddScoped<ICommandHandler, LoginCommandHandler>()` in `Program.cs`

---

## 4. Unit Tests — `LoginCodeStore`

- [ ] 4.1 `GenerateCode` returns a 6-digit code string and a non-empty hex token
- [ ] 4.2 `TryVerify` returns true for valid code + correct `chatId` within TTL; subsequent `TryGetSession` returns true with the same `chatId`
- [ ] 4.3 `TryVerify` returns false for unknown code
- [ ] 4.4 `TryVerify` returns false for correct code but wrong `chatId`
- [ ] 4.5 `TryGetSession` returns false for unknown token
- [ ] 4.6 `TryGetSession` returns false when token TTL has elapsed (use fake `TimeProvider`)
- [ ] 4.7 `Purge` removes the code; subsequent `TryVerify` returns false

---

## 5. Unit Tests — `LoginCommandHandler`

- [ ] 5.1 No code argument → replies `login.usage`; `TryVerify` not called
- [ ] 5.2 Valid code → `TryVerify` returns true → replies `login.success`
- [ ] 5.3 Invalid code → `TryVerify` returns false → replies `login.invalid`
- [ ] 5.4 Code with surrounding whitespace is trimmed before `TryVerify`

---

## 6. Integration Test (Bot-side)

- [ ] 6.1 Wire `LoginCommandHandler` and `LoginCodeStore` (with `TimeProvider.System`) into `TelegramIntegrationTestBase`
- [ ] 6.2 `/login` with no code → bot replies `login.usage`
- [ ] 6.3 `/login BADCODE` → bot replies `login.invalid`
- [ ] 6.4 Generate valid code via `LoginCodeStore.GenerateCode`; dispatch `/login <code>` → bot replies `login.success`; `TryGetSession(token)` returns true with correct `chatId`

---

## 7. New Project: `ProductTrackerBot.Web`

- [ ] 7.1 Create `ProductTrackerBot.Web/ProductTrackerBot.Web.csproj` (target `net9.0`, project-ref to `ProductTrackerBot`)
- [ ] 7.2 Add project to solution: `dotnet sln add ProductTrackerBot.Web/ProductTrackerBot.Web.csproj`
- [ ] 7.3 Create `ProductTrackerBot.Web/Program.cs` with:
  - `LoginCodeStore` + `TimeProvider.System` singleton registrations
  - `GroupRepository`, `PreferenceRepository` registrations
  - `WebSessionStore` singleton + `IHttpContextAccessor`
  - Cookie auth setup (see design.md §DI Registration)
  - `AddRazorComponents().AddInteractiveServerComponents()`
  - `GET /api/auth/status` minimal API endpoint
  - `POST /api/auth/signin` minimal API endpoint (signs in the cookie)
  - `app.UseAuthentication(); app.UseAuthorization();`
  - `app.MapRazorComponents<App>().AddInteractiveServerRenderMode()`

---

## 8. Blazor Shell

- [ ] 8.1 Create `wwwroot/app.css` with Tailwind CDN-compatible `@layer components` classes (`btn-primary`, `btn-secondary`, `btn-danger`, `card`, `input-field`, `nav-link`, `table-row` — see design.md §CSS)
- [ ] 8.2 Create `Components/App.razor`: `<!DOCTYPE html>` shell with Tailwind CDN `<script>`, `<link href="app.css">`, `<HeadOutlet>`, `<Routes>`
- [ ] 8.3 Create `Components/Routes.razor`: `<Router AppAssembly="...">` with `<Found>` → `<RouteView DefaultLayout="MainLayout">` and `<NotFound>` → redirect to `/`
- [ ] 8.4 Create `Components/Layout/MainLayout.razor`: flex container, `<NavMenu>`, `<main>` with `@Body`
- [ ] 8.5 Create `Components/Layout/NavMenu.razor`: sidebar nav with `<NavLink>` for each future page (List, History, Prices, Meals, Week, Settings, AI); active link uses `ActiveClass="bg-indigo-50 text-indigo-600 font-medium"`

---

## 9. Auth Services & Base Class

- [ ] 9.1 Create `ProductTrackerBot.Web/Services/WebSessionStore.cs`: reads `ClaimTypes.NameIdentifier` from `IHttpContextAccessor` → exposes `long ChatId` and `bool IsAuthenticated`
- [ ] 9.2 Create `Components/AuthenticatedPageBase.cs`: injects `NavigationManager` + `WebSessionStore`; in `OnInitialized` redirects to `/login` if not authenticated

---

## 10. Login & Dashboard Pages

- [ ] 10.1 Create `Components/Pages/Login.razor`:
  - `OnInitializedAsync`: calls `GenerateCode(out token)`, saves in component state; starts polling loop via `Task` + `Task.Delay(2000)`; polls `/api/auth/status?token=<t>`; on valid → `POST /api/auth/signin` then `Nav.NavigateTo("/")`
  - `PeriodicTimer` at 1 s updates countdown display; on 0 shows "Get new code" button
  - Code rendered in `<span class="font-mono text-4xl tracking-widest">`
- [ ] 10.2 Create `Components/Pages/Dashboard.razor`: inherits `AuthenticatedPageBase`; renders a welcome card with links to all feature pages

---

## 11. Docker Compose

- [ ] 11.1 Add `ports: - "8080:8080"` to the `bot` service in `docker-compose.yml`
- [ ] 11.2 Add env vars `ASPNETCORE_URLS=http://+:8080` and `WEB_SESSION_TTL_HOURS=24`
- [ ] 11.3 Confirm `Dockerfile` builds `net9.0` target (non-AOT); update if currently targeting AOT runtime

---

## 12. Smoke Test (manual — do not mark complete without user confirmation)

- [ ] 12.1 `make run` → no errors; navigate to `http://localhost:8080/login` → page renders with a 6-digit code and countdown timer
- [ ] 12.2 Wait for countdown to reach 0 → "Get new code" button appears; click it → new code shown, timer resets
- [ ] 12.3 Send `/login BADCODE` to the bot → bot replies `login.invalid`; page stays on login
- [ ] 12.4 Send `/login <correct-code>` to the bot → bot replies `login.success`; browser redirects to `/` within ~4 s
- [ ] 12.5 Navigate directly to `/list` (or any future page URL) → redirects to `/login` if cookie absent
- [ ] 12.6 Refresh the dashboard → stays logged in (cookie persists)
- [ ] 12.7 Sidebar nav links are all visible; clicking each shows a placeholder page
- [ ] 12.8 Existing bot commands (`/buy`, `/list`, `/meals`, `/history`, `/ai`) all continue working normally
