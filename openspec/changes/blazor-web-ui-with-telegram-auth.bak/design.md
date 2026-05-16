## Solution Structure

```
product-tracker-bot-2.sln
├── ProductTrackerBot/           (existing — bot + shared logic)
├── ProductTrackerBot.Tests/     (existing — unit + integration tests)
└── ProductTrackerBot.Web/       (new — Blazor Server)
    ├── ProductTrackerBot.Web.csproj
    ├── Program.cs
    ├── Components/
    │   ├── App.razor
    │   ├── Routes.razor
    │   ├── Layout/
    │   │   ├── MainLayout.razor
    │   │   └── NavMenu.razor
    │   └── Pages/
    │       ├── Login.razor
    │       ├── Dashboard.razor
    │       ├── ShoppingList.razor
    │       ├── History.razor
    │       ├── Prices.razor
    │       ├── Meals.razor
    │       ├── WeekPlan.razor
    │       ├── Settings.razor
    │       └── AiChat.razor
    ├── Services/
    │   ├── LoginCodeStore.cs
    │   └── WebSessionStore.cs
    └── wwwroot/
        └── app.css             (Tailwind directives + minimal overrides)
```

---

## Authentication Flow

### Sequence Diagram

```
Browser                  Blazor Server             Bot Process          Telegram
  |                           |                         |                  |
  |-- GET /login -----------> |                         |                  |
  |                           | generate 6-digit code   |                  |
  |                           | store: code → pending   |                  |
  |                           |   (token, TTL=5min)     |                  |
  |<-- show code + token ---- |                         |                  |
  |                           |                         |                  |
  |  (user sends bot message) |                         |                  |
  |                           |                    <-- /login 482931 --    |
  |                           |                         |                  |
  |                           | <-- verify code --------|                  |
  |                           |   mark session valid    |                  |
  |                           |   store: token → chatId |                  |
  |                           | ----------------------> reply "Authenticated"|
  |                           |                         |                  |
  |-- GET /api/auth/status?token=<t> ----------------> |                  |
  |<-- { valid: true, chatId } ----------------------- |                  |
  |                           |                         |                  |
  |  set HttpOnly cookie      |                         |                  |
  |-- redirect /dashboard --> |                         |                  |
```

### `LoginCodeStore` (singleton, `ProductTrackerBot.Web`)

```csharp
public sealed class LoginCodeStore
{
    record Entry(string Token, long ChatId, DateTime ExpiresUtc, bool Verified);

    ConcurrentDictionary<string, Entry> _codes = new();    // code → entry
    ConcurrentDictionary<string, Entry> _tokens = new();   // token → entry

    string GenerateCode(out string token)
        // 6-digit numeric code; 32-byte hex token
        // stores in _codes; schedules removal after 5 min via CancellationTokenSource

    bool TryVerify(string code, long chatId)
        // looks up _codes[code], checks expiry, sets Verified = true, moves to _tokens

    bool TryGetSession(string token, out long chatId)
        // looks up _tokens[token], checks TTL (default 24 h)

    void Expire(string code) // called by background sweep or CancellationTokenSource callback
}
```

### Bot-side: `LoginCommandHandler` (in `ProductTrackerBot`)

```
Command = "/login"

HandleAsync(message):
  parts = message.Text.Split(' ')
  if parts.Length < 2:
    reply localizer.Get(chatId, "login.usage")
    return
  code = parts[1].Trim()
  success = loginCodeStore.TryVerify(code, message.Chat.Id)
  if success:
    reply localizer.Get(chatId, "login.success")
  else:
    reply localizer.Get(chatId, "login.invalid")
```

`LoginCodeStore` must be accessible from `LoginCommandHandler`. Since the bot and web run in the same process (reference deployment), the singleton is registered once in `Program.cs` and shared. If ever split into separate processes, this becomes an HTTP call — but that is explicitly out of scope here.

### Polling endpoint

`/api/auth/status` is a minimal API endpoint (not a Razor page):

```csharp
app.MapGet("/api/auth/status", (string token, LoginCodeStore store) =>
{
    if (store.TryGetSession(token, out var chatId))
        return Results.Ok(new { valid = true, chatId });
    return Results.Ok(new { valid = false });
});
```

The browser polls this every 2 s from `Login.razor` using `HttpClient` injected via DI.

---

## Session Cookie

On redirect after polling succeeds:
- Server creates a `FormsAuthentication`-style cookie via `IHttpContextAccessor` + `Response.Cookies.Append`.
- Cookie: `HttpOnly`, `SameSite=Strict`, `Secure` (in production), `MaxAge = WEB_SESSION_TTL_HOURS`.
- The `chatId` is embedded as a claim in a signed `DataProtectionPurpose` token, not stored plain.
- All Blazor pages use `[Authorize]` and a custom `AuthenticationStateProvider` backed by the cookie.

---

## Shared `chatId` Context

The web uses a single `chatId` concept (the authenticated user's Telegram chat ID) to scope all repository queries. Since the bot already uses `chatId` as the group/user identifier, the web reads and writes using the same IDs — no new user model needed.

For group-scoped data (shopping list, meals, week plan): the web reads from the same `GroupId` resolved by `GroupRepository.GetOrCreateAsync(chatId)`.

---

## Page Designs

### `/login`

```
┌─────────────────────────────────────┐
│  ProductTracker                     │
│                                     │
│  Sign in via Telegram               │
│                                     │
│  1. Your code:  [ 4 8 2 9 3 1 ]    │
│  2. Open Telegram and send:         │
│     /login 482931  →  @YourBot      │
│  3. This page will redirect         │
│     automatically.                  │
│                                     │
│  [Code expires in 4:32]             │
└─────────────────────────────────────┘
```

- Code displayed in a monospaced, large-font block.
- Countdown timer (JS `setInterval` via `IJSRuntime`).
- After expiry, a "Get new code" button replaces the countdown.

### `/list` (Shopping List)

```
┌────────────────────────────────────────────────┐
│ Shopping List                   [+ Add item]   │
│                                                │
│ ☐  Milk           2 L    exp 2026-05-20   ✎ ✕ │
│ ☐  Eggs           12     exp —            ✎ ✕ │
│ ☑  Bread (done)   1 loaf                  ✕   │
└────────────────────────────────────────────────┘
```

- Inline edit: click ✎ → row becomes an input; Save/Cancel buttons appear.
- Mark done: checkbox → calls `ShoppingListService.MarkDoneAsync`.
- Delete: ✕ with confirmation popover (no modal).
- Add item: inline form slides open at top; same fields as `/buy` flow.

### `/history`

Table with columns: Date, Item, Qty, Price, Shop. Sticky header. Filter bar (date range, item name substring). Pagination: 50 rows/page with Prev/Next buttons.

### `/prices`

Per-item card grid. Each card: item name, last price, trend arrow, mini sparkline (SVG, drawn inline from price history array). Click card → detail list of all prices.

### `/meals`

Two-column layout: meal list on left, detail panel on right. Meal detail shows ingredients (editable table) and steps (reorderable list with drag handle). Add/edit/delete via inline forms.

### `/week`

7-column week grid (Mon–Sun). Each cell shows assigned meal name or "—". Click cell → dropdown of all meals + "Clear" option. No drag-and-drop in v1.

### `/settings`

Form with two fields: Language (select: EN/RU/PL) and Expiry notice (days ahead, number input). Save button. Uses `PreferenceRepository` directly.

### `/ai`

Chat bubble UI. User types message, sends with Enter or button. Response streams in (or appears when complete). Backed by `AiQueryService.QueryAsync`. Message history held in component state (not persisted).

---

## New Project: `ProductTrackerBot.Web.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ProductTrackerBot\ProductTrackerBot.csproj" />
  </ItemGroup>
</Project>
```

No additional NuGet packages. Tailwind via CDN `<script>` in `App.razor` head.

---

## DI Registration (`ProductTrackerBot.Web/Program.cs`)

```csharp
// Shared infrastructure
builder.Services.AddSingleton<LoginCodeStore>();
builder.Services.AddSingleton<WebSessionStore>();   // wraps LoginCodeStore for auth state

// Shared repositories (from ProductTrackerBot)
builder.Services.AddScoped<ShoppingItemRepository>();
builder.Services.AddScoped<PurchaseHistoryRepository>();
builder.Services.AddScoped<PriceLogRepository>();
builder.Services.AddScoped<MealRepository>();
builder.Services.AddScoped<MealIngredientRepository>();
builder.Services.AddScoped<MealStepRepository>();
builder.Services.AddScoped<DayMealsRepository>();
builder.Services.AddScoped<GroupRepository>();
builder.Services.AddSingleton<IPreferenceRepository, PreferenceRepository>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<MealMergeService>();
builder.Services.AddScoped<IAiQueryService, AiQueryService>();

// Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt => { opt.LoginPath = "/login"; opt.Cookie.HttpOnly = true; });
builder.Services.AddAuthorization();

// Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
```

The bot's `Program.cs` also registers `LoginCodeStore` as a singleton so `LoginCommandHandler` can resolve it. Both processes share the same DI root because they run in the same host in the reference deployment.

---

## Bot integration: shared host

```
ProductTrackerBot/Program.cs  (existing)
  ↓ also registers:
  builder.Services.AddSingleton<LoginCodeStore>();   // ← new
  builder.Services.AddScoped<ICommandHandler, LoginCommandHandler>(); // ← new
```

In the reference deployment (single process), `WebApplication.CreateBuilder` is replaced with a combined host that runs both the bot `IHostedService` and the Blazor `WebApplication`. The simplest approach: launch the web host as a second `IHostedService` from the bot's host. This keeps the DI container unified.

Alternative (two processes, shared SQLite): requires SQLite WAL mode and a shared login-code API endpoint. Out of scope for v1.

---

## UI Component Conventions

- Every page component inherits from `AuthenticatedPageBase` (a base class that checks the auth cookie and redirects to `/login` if missing).
- State mutation calls the repository directly from the component's event handler; after mutation, `StateHasChanged()` is called.
- No Flux/Redux pattern — component-local state is sufficient for household scale.
- Error messages rendered inline via a `<div class="text-red-600 text-sm">` banner at the top of the form.
- Loading state shown via a `loading` bool toggled before/after async calls; renders a spinner `<div class="animate-spin ...">`.

---

## CSS / Tailwind Conventions

```html
<!-- App.razor <head> -->
<script src="https://cdn.tailwindcss.com"></script>
```

Custom classes defined in `wwwroot/app.css` using `@layer components`:

```css
@layer components {
  .btn-primary   { @apply bg-indigo-600 text-white px-4 py-2 rounded-md text-sm font-medium hover:bg-indigo-700; }
  .btn-secondary { @apply bg-white text-gray-700 border border-gray-300 px-4 py-2 rounded-md text-sm font-medium hover:bg-gray-50; }
  .btn-danger    { @apply bg-red-600 text-white px-4 py-2 rounded-md text-sm font-medium hover:bg-red-700; }
  .card          { @apply bg-white rounded-lg shadow-sm p-6; }
  .table-row     { @apply border-b border-gray-100 hover:bg-gray-50; }
  .input-field   { @apply block w-full rounded-md border-gray-300 shadow-sm text-sm focus:border-indigo-500 focus:ring-indigo-500; }
}
```

---

## Localization Keys (new in `Strings.*.json`)

| Key | English |
|---|---|
| `login.usage` | `Send /login <code> with the code shown on the website.` |
| `login.success` | `Authenticated! You can return to the browser.` |
| `login.invalid` | `Invalid or expired code. Please reload the login page and try again.` |

---

## AOT Compatibility Note

`ProductTrackerBot.Web` is a Blazor Server project and is **not AOT-compiled** — this is intentional and acceptable. The shared `ProductTrackerBot` class library must remain AOT-safe (no reflection, source-gen JSON). The web project's `Program.cs` uses standard ASP.NET reflection-based DI which is fine for a non-AOT target.

---

## Docker Compose Change

```yaml
# docker-compose.yml additions
services:
  bot:
    build: .
    ports:
      - "8080:8080"          # new: expose Blazor port
    environment:
      - BOT_TOKEN=${BOT_TOKEN}
      - WEB_SESSION_TTL_HOURS=24
      - ASPNETCORE_URLS=http://+:8080
    volumes:
      - ./data:/app/data
```

A single container runs both the bot and the web host. The `Dockerfile` targets `net9.0` (non-AOT build).
