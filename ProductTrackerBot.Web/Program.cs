using System.Net.Http.Headers;
using System.Security.Claims;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using ProductTrackerBot;
using ProductTrackerBot.Database;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using ProductTrackerBot.Web.Components;
using ProductTrackerBot.Web.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;

// Load .env (same logic as the bot)
var envFile = Path.Combine(Environment.CurrentDirectory, ".env");
if (!File.Exists(envFile))
    envFile = Path.Combine(AppContext.BaseDirectory, "../../../../.env");
if (File.Exists(envFile))
    Env.Load(envFile);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// ── Bot configuration ────────────────────────────────────────────────────────
builder.Services.AddOptions<BotConfiguration>()
    .BindConfiguration(string.Empty)
    .PostConfigure(config =>
    {
        if (string.IsNullOrWhiteSpace(config.Token))
            throw new InvalidOperationException("BOT_TOKEN is required.");
    })
    .ValidateOnStart();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptionsMonitor<BotConfiguration>>();
    return new TelegramBotClient(opts.CurrentValue.Token);
});

// ── Database ─────────────────────────────────────────────────────────────────
var dbPath = builder.Configuration["DB_PATH"]
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../data/product-tracker.db"));
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
var connectionString = $"Data Source={dbPath}";
builder.Services.AddSingleton(connectionString);
builder.Services.AddHostedService<DatabaseInitializer>();

// ── AI / OpenRouter ───────────────────────────────────────────────────────────
var aiQueryOptions = new AiQueryOptions
{
    ApiKey = builder.Configuration["OPENROUTER_API_KEY"] ?? string.Empty,
    Model = builder.Configuration["AI_QUERY_MODEL"] ?? "openai/gpt-4o-mini",
    BaseUrl = "https://openrouter.ai/api/v1/",
    IdentityMdPath = Path.Combine(AppContext.BaseDirectory, "IDENTITY.md"),
};
builder.Services.AddSingleton(aiQueryOptions);
builder.Services.AddHttpClient("openrouter", (sp, client) =>
{
    var opts = sp.GetRequiredService<AiQueryOptions>();
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    if (!string.IsNullOrEmpty(opts.ApiKey))
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);
    client.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/product-tracker-bot");
    client.DefaultRequestHeaders.Add("X-Title", "ProductTrackerBot");
});
builder.Services.AddSingleton<IOpenRouterClient>(sp =>
    new OpenRouterClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("openrouter"),
        sp.GetRequiredService<AiQueryOptions>()));

// ── Bot infrastructure ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IUpdateHandler, UpdateDispatcher>();
builder.Services.AddHostedService<BotCommandRegistrationService>(sp =>
{
    var botClient = sp.GetRequiredService<ITelegramBotClient>();
    var logger = sp.GetRequiredService<ILogger<BotCommandRegistrationService>>();
    using var scope = sp.CreateScope();
    var handlers = scope.ServiceProvider.GetRequiredService<IEnumerable<ICommandHandler>>();
    return new BotCommandRegistrationService(botClient, handlers, logger);
});
builder.Services.AddHostedService<BotHostedService>();

// ── Shared singletons ─────────────────────────────────────────────────────────
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LoginCodeStore>();
builder.Services.AddSingleton<PendingAddService>();
builder.Services.AddSingleton<PendingEditService>();
builder.Services.AddSingleton<ConversationHistoryService>();
builder.Services.AddSingleton<AiSuggestionService>();

// ── Dialog state services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<PendingDialogService<BuyDialogState>>();
builder.Services.AddSingleton<PendingDialogService<EditItemDialogState>>();
builder.Services.AddSingleton<PendingDialogService<PriceCaptureDialogState>>();
builder.Services.AddSingleton<PendingDialogService<MealCreateDialogState>>();
builder.Services.AddSingleton<PendingDialogService<MealAddIngredientDialogState>>();
builder.Services.AddSingleton<PendingDialogService<MealAddStepDialogState>>();

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<GroupRepository>();
builder.Services.AddScoped<ShoppingItemRepository>();
builder.Services.AddSingleton<IHistoryRepository, HistoryRepository>();
builder.Services.AddScoped<PurchaseHistoryRepository>();
builder.Services.AddScoped<PriceLogRepository>();
builder.Services.AddScoped<MealRepository>();
builder.Services.AddScoped<MealIngredientRepository>();
builder.Services.AddScoped<MealStepRepository>();
builder.Services.AddSingleton<IPreferenceRepository, PreferenceRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAiQueryService, AiQueryService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<MealMergeService>();
builder.Services.AddScoped<ExpiryNotificationService>();
builder.Services.AddScoped<IUndoService, UndoService>();
builder.Services.AddSingleton<ILocalizer, Localizer>();

// ── Background jobs ───────────────────────────────────────────────────────────
var notifyTimeUtc = Environment.GetEnvironmentVariable("NOTIFY_TIME_UTC") ?? "09:00";
builder.Services.AddSingleton(sp => new ExpiryNotificationJob(
    sp.GetRequiredService<ITelegramBotClient>(),
    sp.GetRequiredService<GroupRepository>(),
    sp.GetRequiredService<ExpiryNotificationService>(),
    sp.GetRequiredService<ILogger<ExpiryNotificationJob>>(),
    notifyTimeUtc));
builder.Services.AddHostedService(sp => sp.GetRequiredService<ExpiryNotificationJob>());

// ── Command handlers ──────────────────────────────────────────────────────────
builder.Services.AddScoped<ICommandHandler, AiCommandHandler>();
builder.Services.AddScoped<ICommandHandler, LoginCommandHandler>();
builder.Services.AddScoped<ICommandHandler, BuyCommandHandler>();
builder.Services.AddScoped<ICommandHandler, ListCommandHandler>();
builder.Services.AddScoped<ICommandHandler, HistoryCommandHandler>();
builder.Services.AddScoped<ICommandHandler, SearchCommandHandler>();
builder.Services.AddScoped<ICommandHandler, LanguageCommandHandler>();
builder.Services.AddScoped<ICommandHandler, MealsCommandHandler>();
builder.Services.AddScoped<ICommandHandler, PricesCommandHandler>();
builder.Services.AddScoped<ICommandHandler, SettingsCommandHandler>();
builder.Services.AddScoped<ICommandHandler, StartCommandHandler>();
builder.Services.AddScoped<ICommandHandler, UndoCommandHandler>();

// ── Dialog message handlers ───────────────────────────────────────────────────
builder.Services.AddScoped<IDialogMessageHandler, BuyStepHandler>();
builder.Services.AddScoped<IDialogMessageHandler, PriceCaptureStepHandler>();
builder.Services.AddScoped<IDialogMessageHandler, MealDialogStepHandler>();
builder.Services.AddScoped<IDialogMessageHandler, ItemEditStepHandler>();

// ── Callback handlers ─────────────────────────────────────────────────────────
builder.Services.AddScoped<ICallbackHandler, BuySkipCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, BuyConfirmCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, BuyEditCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, BuyCancelCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ItemEditCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ItemSaveCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ItemCancelEditCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ShopDoneCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ShopRemoveCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, PriceSkipCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, PriceShopSuggestionCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, LanguageCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ListNextCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ListPrevCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, MealCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ActionCancelCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, UndoInlineCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, AiAddItemCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, AiAddAllCallbackHandler>();

// ── Web auth ──────────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<WebSessionStore>();

var sessionTtlHours = int.Parse(Environment.GetEnvironmentVariable("WEB_SESSION_TTL_HOURS") ?? "24");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(sessionTtlHours);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();

// ── Blazor ────────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/api/auth/status", (string token, LoginCodeStore store) =>
    store.TryGetSession(token, out var chatId)
        ? Results.Ok(new { valid = true, chatId })
        : Results.Ok(new { valid = false, chatId = 0L }));

app.MapGet("/api/auth/complete", async (HttpContext ctx, string token, LoginCodeStore store) =>
{
    if (!store.TryGetSession(token, out var chatId))
        return Results.Redirect("/login");

    var claims = new[] { new Claim(ClaimTypes.NameIdentifier, chatId.ToString()) };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return Results.Redirect("/");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
