## Solution Structure

```
product-tracker-bot-2.sln
├── ProductTrackerBot/           (existing — unchanged except LoginCodeStore + LoginCommandHandler)
├── ProductTrackerBot.Tests/     (existing)
└── ProductTrackerBot.Web/       (new — Blazor Server)
    ├── ProductTrackerBot.Web.csproj
    ├── Program.cs
    ├── Components/
    │   ├── App.razor
    │   ├── Routes.razor
    │   ├── AuthenticatedPageBase.cs
    │   ├── Layout/
    │   │   ├── MainLayout.razor
    │   │   └── NavMenu.razor
    │   └── Pages/
    │       ├── Login.razor
    │       └── Dashboard.razor    (placeholder — redirects to /list)
    ├── Services/
    │   └── WebSessionStore.cs
    └── wwwroot/
        └── app.css
```

---

## Authentication Flow

### Sequence Diagram

```
Browser                  Blazor Server             Bot Process
  |                           |                         |
  |-- GET /login -----------> |                         |
  |                           | GenerateCode(out token) |
  |                           | _codes[code] = Entry    |
  |                           |   (token, pending, 5m)  |
  |<-- code + token shown --- |                         |
  |                           |                         |
  |  (user opens Telegram)    |                         |
  |                           |              /login 482931 from user
  |                           |                         |
  |                           | <-- TryVerify(code, chatId)
  |                           |   moves entry to _tokens|
  |                           | ----------------------> reply "Authenticated"
  |                           |                         |
  |-- GET /api/auth/status?token=<t> ---------------->  |
  |<-- { valid: true, chatId } ----------------------   |
  |  set signed HttpOnly cookie                         |
  |-- redirect / ------->     |                         |
```

---

## `LoginCodeStore` (singleton in `ProductTrackerBot`)

```csharp
// ProductTrackerBot/Services/LoginCodeStore.cs
public sealed class LoginCodeStore
{
    private record Entry(string Token, long ChatId, DateTime ExpiresUtc, bool Verified);

    private readonly ConcurrentDictionary<string, Entry> _codes  = new();   // code  → entry
    private readonly ConcurrentDictionary<string, Entry> _tokens = new();   // token → entry
    private readonly TimeProvider _time;

    // Injected via constructor; defaults to TimeProvider.System for production
    public LoginCodeStore(TimeProvider time) { _time = time; }

    public string GenerateCode(out string token)
        // Returns 6-digit zero-padded string
        // token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
        // Stores: _codes[code] = new Entry(token, 0, UtcNow + 5min, false)
        // Schedules Purge(code) via CancellationTokenSource + Task.Delay

    public bool TryVerify(string code, long chatId)
        // Lookup _codes[code]; if missing or expired return false
        // Remove from _codes; store verified entry in _tokens[token] with 24 h TTL
        // Return true

    public bool TryGetSession(string token, out long chatId)
        // Lookup _tokens[token]; if missing or expired set chatId=0 return false
        // Set chatId = entry.ChatId; return true

    public void Purge(string code)
        // _codes.TryRemove(code, out _)
}
```

`TimeProvider` injection makes the store testable without real sleeps.

---

## `LoginCommandHandler` (in `ProductTrackerBot`)

```
Command = "/login"

HandleAsync(message):
  parts = message.Text?.Split(' ', 2) ?? []
  if parts.Length < 2 or string.IsNullOrWhiteSpace(parts[1]):
    await botClient.SendMessage(chatId, localizer.Get(chatId, "login.usage"))
    return
  code = parts[1].Trim()
  if loginCodeStore.TryVerify(code, message.Chat.Id):
    await botClient.SendMessage(chatId, localizer.Get(chatId, "login.success"))
  else:
    await botClient.SendMessage(chatId, localizer.Get(chatId, "login.invalid"))
```

No `HistoryRepository.RecordAsync` — login is not an inventory mutation.

---

## Polling Endpoint

```csharp
// In ProductTrackerBot.Web/Program.cs
app.MapGet("/api/auth/status", (string token, LoginCodeStore store) =>
{
    return store.TryGetSession(token, out var chatId)
        ? Results.Ok(new { valid = true, chatId })
        : Results.Ok(new { valid = false, chatId = 0L });
});
```

Not behind `[Authorize]` — it is the pre-auth endpoint.

---

## Session Cookie

After `/api/auth/status` returns `valid: true`, `Login.razor` calls a server-side endpoint that:
1. Creates a `ClaimsPrincipal` with a single claim: `ClaimTypes.NameIdentifier = chatId.ToString()`.
2. Calls `HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)`.
3. Returns `Results.Ok()`.

Cookie settings:
```csharp
options.Cookie.HttpOnly = true;
options.Cookie.SameSite = SameSiteMode.Strict;
options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
options.ExpireTimeSpan = TimeSpan.FromHours(sessionTtlHours);
options.SlidingExpiration = false;
```

The sign-in endpoint is `POST /api/auth/signin` and accepts `{ token }` in the body. `Login.razor` calls it via `HttpClient` once polling confirms `valid: true`.

---

## `WebSessionStore` (singleton in `ProductTrackerBot.Web`)

```csharp
// ProductTrackerBot.Web/Services/WebSessionStore.cs
public sealed class WebSessionStore(IHttpContextAccessor accessor)
{
    public long ChatId =>
        long.Parse(accessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public bool IsAuthenticated =>
        accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
```

Injected into pages to get the current `chatId` without reading the cookie directly.

---

## `AuthenticatedPageBase`

```csharp
// ProductTrackerBot.Web/Components/AuthenticatedPageBase.cs
public abstract class AuthenticatedPageBase : ComponentBase
{
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected WebSessionStore Session { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (!Session.IsAuthenticated)
            Nav.NavigateTo("/login", forceLoad: true);
    }
}
```

All feature pages inherit this class.

---

## `Login.razor` Behaviour

1. `OnInitializedAsync`: calls `LoginCodeStore.GenerateCode(out token)`, stores both in component fields.
2. Renders a large monospace code block + instructional text.
3. Starts a polling `Task` (using `InvokeAsync` + `Task.Delay(2000)`) hitting `/api/auth/status?token=<t>` via `HttpClient`.
4. On valid response: calls `POST /api/auth/signin` with `{ token }`, then `Nav.NavigateTo("/", forceLoad: true)`.
5. Countdown: uses `PeriodicTimer` (1 s interval) to decrement a display counter; on expiry shows "Get new code" button which calls `GenerateCode` again.

---

## Project File

```xml
<!-- ProductTrackerBot.Web/ProductTrackerBot.Web.csproj -->
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

---

## DI Registration (`ProductTrackerBot.Web/Program.cs`)

```csharp
// Shared with bot
builder.Services.AddSingleton<LoginCodeStore>();
builder.Services.AddSingleton(TimeProvider.System);

// Repositories (shared, scoped)
builder.Services.AddScoped<GroupRepository>();
builder.Services.AddSingleton<IPreferenceRepository, PreferenceRepository>();
// (remaining repos added in blazor-web-inventory-pages / blazor-web-advanced-pages)

// Web-only
builder.Services.AddSingleton<WebSessionStore>();
builder.Services.AddHttpContextAccessor();

// Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Strict;
        opt.ExpireTimeSpan = TimeSpan.FromHours(
            int.Parse(Environment.GetEnvironmentVariable("WEB_SESSION_TTL_HOURS") ?? "24"));
    });
builder.Services.AddAuthorization();

// Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
```

The bot's `Program.cs` also gains:
```csharp
builder.Services.AddSingleton<LoginCodeStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICommandHandler, LoginCommandHandler>();
```

---

## Layout: `MainLayout.razor`

```razor
@inherits LayoutComponentBase
<div class="flex h-screen bg-gray-50">
    <NavMenu />
    <main class="flex-1 overflow-y-auto p-8">
        <div class="max-w-5xl mx-auto">
            @Body
        </div>
    </main>
</div>
```

## Layout: `NavMenu.razor`

```razor
<nav class="w-56 bg-white border-r border-gray-200 flex flex-col py-6 px-4 gap-1">
    <span class="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">Menu</span>
    <NavLink href="/" class="nav-link">Dashboard</NavLink>
    <NavLink href="/list" class="nav-link">Shopping List</NavLink>
    <NavLink href="/history" class="nav-link">History</NavLink>
    <NavLink href="/prices" class="nav-link">Prices</NavLink>
    <NavLink href="/meals" class="nav-link">Meals</NavLink>
    <NavLink href="/week" class="nav-link">Week Plan</NavLink>
    <NavLink href="/settings" class="nav-link">Settings</NavLink>
    <NavLink href="/ai" class="nav-link">AI Query</NavLink>
</nav>
```

Active link class: `bg-indigo-50 text-indigo-600 font-medium` (set via `NavLink`'s `ActiveClass`).

---

## CSS (`wwwroot/app.css`)

```css
@layer components {
  .btn-primary   { @apply bg-indigo-600 text-white px-4 py-2 rounded-md text-sm font-medium hover:bg-indigo-700 transition-colors; }
  .btn-secondary { @apply bg-white text-gray-700 border border-gray-300 px-4 py-2 rounded-md text-sm font-medium hover:bg-gray-50; }
  .btn-danger    { @apply bg-red-600 text-white px-4 py-2 rounded-md text-sm font-medium hover:bg-red-700; }
  .card          { @apply bg-white rounded-lg shadow-sm p-6; }
  .input-field   { @apply block w-full rounded-md border-gray-300 shadow-sm text-sm focus:border-indigo-500 focus:ring-indigo-500 px-3 py-2 border; }
  .nav-link      { @apply flex items-center gap-2 px-3 py-2 rounded-md text-sm text-gray-600 hover:bg-gray-100 hover:text-gray-900 transition-colors; }
  .table-row     { @apply border-b border-gray-100 hover:bg-gray-50; }
}
```

---

## Docker Compose

```yaml
services:
  bot:
    build: .
    ports:
      - "8080:8080"
    environment:
      - BOT_TOKEN=${BOT_TOKEN}
      - WEB_SESSION_TTL_HOURS=24
      - ASPNETCORE_URLS=http://+:8080
    volumes:
      - ./data:/app/data
```

Single container runs both the bot `IHostedService` and the Blazor `WebApplication` on the same .NET generic host.

---

## Localization Keys

| Key | EN | RU | PL |
|---|---|---|---|
| `login.usage` | `Send /login <code> with the code shown on the website.` | `Отправьте /login <код> с кодом, показанным на сайте.` | `Wyślij /login <kod> z kodem widocznym na stronie.` |
| `login.success` | `Authenticated! You can return to the browser.` | `Авторизация прошла успешно! Можете вернуться в браузер.` | `Uwierzytelniono! Możesz wrócić do przeglądarki.` |
| `login.invalid` | `Invalid or expired code. Reload the login page and try again.` | `Неверный или истёкший код. Обновите страницу входа и попробуйте снова.` | `Nieprawidłowy lub wygasły kod. Odśwież stronę logowania i spróbuj ponownie.` |

---

## AOT Note

`ProductTrackerBot.Web` is not AOT-compiled (Blazor Server requires reflection). The shared `ProductTrackerBot` library must remain AOT-safe: `LoginCodeStore` uses `ConcurrentDictionary`, `RandomNumberGenerator`, and `CancellationTokenSource` — all trimming-safe.
