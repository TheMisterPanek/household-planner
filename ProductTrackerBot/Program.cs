using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using ProductTrackerBot;
using ProductTrackerBot.Database;
using Telegram.Bot;
using Telegram.Bot.Polling;

// Load environment variables from .env file before creating the host
// AppContext.BaseDirectory is bin/Debug/netX.X/ so ../../../ is the project root
var envFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.env"));
Env.Load(envFile);

var builder = Host.CreateApplicationBuilder(args);

// Configure BotConfiguration from environment variables
builder.Services.AddOptions<BotConfiguration>()
    .BindConfiguration(string.Empty)
    .ValidateOnStart();

// Add custom validation for Token
// Note: BotConfiguration.Token should never be logged; ensure no logging middleware logs configuration
builder.Services.AddOptions<BotConfiguration>()
    .PostConfigure(config =>
    {
        if (string.IsNullOrWhiteSpace(config.Token))
        {
            throw new InvalidOperationException("BotConfiguration.Token is required and cannot be empty. Set BOT_TOKEN environment variable.");
        }
    });

// Configure structured JSON logging with UTC timestamps
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

// Register Telegram bot services
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<BotConfiguration>>();
    return new TelegramBotClient(options.CurrentValue.Token);
});

// Register SQLite database path and initializer
var dbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../data/product-tracker.db"));
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
var connectionString = $"Data Source={dbPath}";
builder.Services.AddSingleton(connectionString);
builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddSingleton<IUpdateHandler, UpdateDispatcher>();
builder.Services.AddHostedService<BotHostedService>();

var host = builder.Build();
await host.RunAsync();

