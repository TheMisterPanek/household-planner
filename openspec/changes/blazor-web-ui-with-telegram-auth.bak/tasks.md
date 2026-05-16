## 1. Localization Keys (Bot-side)

- [ ] 1.1 Add `login.usage`, `login.success`, `login.invalid` keys to `Strings.en.json`
- [ ] 1.2 Add the same keys with Russian translations to `Strings.ru.json`
- [ ] 1.3 Add the same keys with Polish translations to `Strings.pl.json`

---

## 2. `LoginCodeStore` (shared singleton)

- [ ] 2.1 Create `ProductTrackerBot/Services/LoginCodeStore.cs`:
  - Internal record `Entry(string Token, long ChatId, DateTime ExpiresUtc, bool Verified)`
  - `string GenerateCode(out string token)` — 6-digit numeric code, 32-byte hex token; stored in `_codes` ConcurrentDictionary with 5-minute expiry via `CancellationTokenSource`
  - `bool TryVerify(string code, long chatId)` — checks expiry, sets `Verified = true`, moves entry to `_tokens` keyed by token; returns `false` if code missing/expired
  - `bool TryGetSession(string token, out long chatId)` — checks `_tokens`, validates TTL (24 h default, configurable via `WEB_SESSION_TTL_HOURS` env var)
  - `void Purge(string code)` — removes from `_codes` (called by expiry callback)
- [ ] 2.2 Register `LoginCodeStore` as singleton in `ProductTrackerBot/Program.cs`

---

## 3. `LoginCommandHandler` (bot-side)

- [ ] 3.1 Create `ProductTrackerBot/Handlers/LoginCommandHandler.cs` implementing `ICommandHandler`:
  - `Command = "/login"`
  - Parse `parts = message.Text.Split(' ')`; if `parts.Length < 2` reply `localizer.Get(chatId, "login.usage")` and return
  - Call `loginCodeStore.TryVerify(parts[1].Trim(), message.Chat.Id)`
  - On success: reply `localizer.Get(chatId, "login.success")`
  - On failure: reply `localizer.Get(chatId, "login.invalid")`
  - No `IHistoryRepository.RecordAsync` call (auth is not an inventory mutation)
- [ ] 3.2 Register `builder.Services.AddScoped<ICommandHandler, LoginCommandHandler>()` in `Program.cs`

---

## 4. Unit Tests — `LoginCommandHandler`

- [ ] 4.1 No code argument → replies `login.usage`; `TryVerify` never called
- [ ] 4.2 Valid code → `TryVerify` returns true → replies `login.success`
- [ ] 4.3 Invalid/expired code → `TryVerify` returns false → replies `login.invalid`
- [ ] 4.4 Code with extra whitespace is trimmed before `TryVerify`

---

## 5. Unit Tests — `LoginCodeStore`

- [ ] 5.1 `GenerateCode` returns a 6-digit string and a 32-byte hex token
- [ ] 5.2 `TryVerify` returns true for a valid code + correct chatId within TTL
- [ ] 5.3 `TryVerify` returns false for an unknown code
- [ ] 5.4 `TryVerify` returns false for a correct code but wrong chatId
- [ ] 5.5 After successful `TryVerify`, `TryGetSession` returns true with the correct chatId
- [ ] 5.6 `TryGetSession` returns false for an unknown token
- [ ] 5.7 `TryGetSession` returns false for a token whose TTL has elapsed (use `ISystemClock` or `TimeProvider` for testability)
- [ ] 5.8 `Purge` removes the code so subsequent `TryVerify` returns false

---

## 6. New Project: `ProductTrackerBot.Web`

- [ ] 6.1 Create `ProductTrackerBot.Web/ProductTrackerBot.Web.csproj` targeting `net9.0`, with `<ProjectReference>` to `ProductTrackerBot`
- [ ] 6.2 Add the project to `product-tracker-bot-2.sln` via `dotnet sln add`
- [ ] 6.3 Create `ProductTrackerBot.Web/Program.cs` with:
  - `LoginCodeStore` singleton registration (shared with bot)
  - All repository/service registrations (see design.md §DI Registration)
  - Cookie authentication setup (`AddAuthentication(...).AddCookie(...)`)
  - `AddRazorComponents().AddInteractiveServerComponents()`
  - Minimal API endpoint: `GET /api/auth/status?token=<t>` returning `{ valid, chatId }`
  - `app.UseAuthentication(); app.UseAuthorization();`
  - `app.MapRazorComponents<App>().AddInteractiveServerRenderMode()`
- [ ] 6.4 Create `wwwroot/app.css` with Tailwind component layer classes (`btn-primary`, `btn-secondary`, `btn-danger`, `card`, `table-row`, `input-field` — see design.md §CSS/Tailwind Conventions)
- [ ] 6.5 Create `Components/App.razor` with `<!DOCTYPE html>` shell, Tailwind CDN script tag, `<HeadOutlet>`, `<Routes>`
- [ ] 6.6 Create `Components/Routes.razor` with `<Router>` pointing to the web assembly
- [ ] 6.7 Create `Components/Layout/MainLayout.razor`:
  - Sidebar nav (links: List, History, Prices, Meals, Week, Settings, AI)
  - Content `@Body` in `<main class="ml-64 p-8 max-w-5xl">`
- [ ] 6.8 Create `Components/Layout/NavMenu.razor`: nav links rendered as `<a>` tags, active link highlighted with `bg-indigo-50 text-indigo-600`

---

## 7. Auth Pages & Base Class

- [ ] 7.1 Create `Services/WebSessionStore.cs` — thin wrapper: resolves `chatId` from the `HttpContext` cookie claim; exposes `long ChatId` for page injection
- [ ] 7.2 Create `Components/AuthenticatedPageBase.cs` (abstract `ComponentBase`):
  - `[Inject] NavigationManager Nav`, `[Inject] WebSessionStore Session`
  - `OnInitializedAsync` checks session; if unauthenticated, `Nav.NavigateTo("/login")`
- [ ] 7.3 Create `Components/Pages/Login.razor`:
  - On init: calls `LoginCodeStore.GenerateCode(out token)`, stores both in component state
  - Displays code in large monospace block and `token` as hidden (used for polling)
  - JS interop countdown timer (calls `setInterval` every second to update display)
  - Polling: `InvokeAsync` loop every 2 s hitting `/api/auth/status?token=<t>` via `HttpClient`
  - On valid response: `NavigationManager.NavigateTo("/")` (sets cookie via server response header)
  - "Get new code" button after expiry calls `LoginCodeStore.GenerateCode` again and resets timer

---

## 8. Dashboard / Shopping List Page

- [ ] 8.1 Create `Components/Pages/Dashboard.razor` (inherits `AuthenticatedPageBase`): simple redirect to `/list` or a welcome card
- [ ] 8.2 Create `Components/Pages/ShoppingList.razor`:
  - Loads items via `ShoppingListService` (resolved for `chatId` from session)
  - Renders table: checkbox, name, qty, expiry, ✎ edit, ✕ delete
  - Inline edit: row toggles to input fields on ✎ click; Save calls `ShoppingItemRepository.UpdateAsync`; cancel restores
  - Add item: collapsible form at top; on submit calls `ShoppingListService.AddItemAsync`
  - Mark done: checkbox → `ShoppingListService.MarkDoneAsync`; row grays out
  - Delete: ✕ button with inline confirmation popover; on confirm calls `ShoppingItemRepository.DeleteAsync`

---

## 9. History Page

- [ ] 9.1 Create `Components/Pages/History.razor`:
  - Loads page 1 of history from `PurchaseHistoryRepository.GetPageAsync(chatId, page, pageSize: 50)`
  - Filter bar: date-from, date-to, item-name text inputs; on change reloads
  - Paginated table: Date, Item, Qty, Price, Shop columns
  - Prev/Next buttons; disabled when at bounds

---

## 10. Prices Page

- [ ] 10.1 Create `Components/Pages/Prices.razor`:
  - Groups price log entries by item name
  - Card grid (3 columns): each card shows item name, last price, trend arrow (↑↓→), inline SVG sparkline
  - Clicking a card expands an inline detail list of all prices with dates

---

## 11. Meals Page

- [ ] 11.1 Create `Components/Pages/Meals.razor`:
  - Two-column layout: meal list sidebar + detail panel
  - Meal list: name + ✎ edit / ✕ delete buttons; [+ New meal] button at top
  - Detail panel: meal name (editable), ingredients table (name, qty, unit — inline add/delete rows), steps list (text — inline add/delete/reorder with up/down arrows)
  - All mutations call the corresponding `Meal*Repository` methods

---

## 12. Weekly Plan Page

- [ ] 12.1 Create `Components/Pages/WeekPlan.razor`:
  - 7-row table (Mon–Sun); each row: day name, assigned meal name (or "—"), dropdown to change
  - Dropdown populated from `MealRepository.GetAllAsync(groupId)` + "— Clear —" option
  - On selection change: calls `DayMealsRepository.UpsertAsync` (or `ClearAsync`); `StateHasChanged()`

---

## 13. Settings Page

- [ ] 13.1 Create `Components/Pages/Settings.razor`:
  - Language select (EN/RU/PL); maps to `PreferenceRepository.SetLanguageAsync`
  - Expiry days input (number); maps to `PreferenceRepository.SetExpiryThresholdAsync` (or existing equivalent)
  - Save button; success message shown inline

---

## 14. AI Chat Page

- [ ] 14.1 Create `Components/Pages/AiChat.razor`:
  - Scrollable message list (user bubbles right-aligned, bot bubbles left-aligned)
  - Input at bottom; submit on Enter or button click
  - On submit: appends user message to list, calls `AiQueryService.QueryAsync(chatId, text)`, appends response
  - Component-local state only (no persistence)

---

## 15. Docker Compose Update

- [ ] 15.1 Update `docker-compose.yml`:
  - Add `ports: - "8080:8080"` to the `bot` service
  - Add `ASPNETCORE_URLS=http://+:8080` env var
  - Add `WEB_SESSION_TTL_HOURS=24` env var
- [ ] 15.2 Update `Dockerfile` (or confirm it targets `net9.0` non-AOT runtime) so both bot and web are built and started in the same image

---

## 16. Integration Tests (Bot-side)

- [ ] 16.1 Wire `LoginCommandHandler` and `LoginCodeStore` into `TelegramIntegrationTestBase`
- [ ] 16.2 `/login` with no code argument → bot replies `login.usage`
- [ ] 16.3 `/login <invalid-code>` → bot replies `login.invalid`
- [ ] 16.4 Generate a valid code via `LoginCodeStore.GenerateCode`, dispatch `/login <code>` → bot replies `login.success`; `LoginCodeStore.TryGetSession(token)` returns true with correct chatId

---

## 17. Smoke Test (manual — do not mark complete without user confirmation)

- [ ] 17.1 `make run` starts without errors; navigate to `http://localhost:8080/login` → login page renders with a 6-digit code and countdown
- [ ] 17.2 Send `/login <wrong-code>` to bot → bot replies `login.invalid`; page stays on login
- [ ] 17.3 Send `/login <correct-code>` to bot → bot replies `login.success`; browser redirects to `/list` within ~4 s
- [ ] 17.4 Shopping list page loads items from DB; Add an item → it appears in the list immediately and in the Telegram `/list` command output
- [ ] 17.5 Mark an item done via checkbox → item grays out; confirm via `/list` in Telegram it no longer appears
- [ ] 17.6 Edit an item name inline → Save → updated name persists after page refresh
- [ ] 17.7 Delete an item with confirmation → item disappears; confirmed gone in Telegram
- [ ] 17.8 Navigate to `/history` → purchase history table loads; filter by item name substring → results narrow correctly
- [ ] 17.9 Navigate to `/meals` → existing meals appear; add a new meal with ingredients → `/meals` in Telegram shows the new meal
- [ ] 17.10 Navigate to `/week` → Mon–Sun grid renders; assign a meal to Wednesday → `/week` in Telegram shows the assignment
- [ ] 17.11 Navigate to `/settings` → change language to Polish → bot replies in Polish to subsequent commands
- [ ] 17.12 Navigate to `/ai` → send a query → bot response appears in chat bubbles
- [ ] 17.13 Session cookie persists across page refresh (no re-login required within TTL)
- [ ] 17.14 After `WEB_SESSION_TTL_HOURS` passes (or manually clear cookie) → accessing any page redirects to `/login`
- [ ] 17.15 Existing bot commands (`/buy`, `/list`, `/meals`, `/history`, `/ai`) all continue working normally
