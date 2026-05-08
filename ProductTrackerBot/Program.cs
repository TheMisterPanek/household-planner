// <copyright file="Program.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using ProductTrackerBot;
using ProductTrackerBot.Database;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
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

// Register dialog state services
builder.Services.AddSingleton<PendingDialogService<BuyDialogState>>();
builder.Services.AddSingleton<PendingDialogService<PriceCaptureDialogState>>();
builder.Services.AddSingleton<PendingDialogService<MealCreateDialogState>>();
builder.Services.AddSingleton<PendingDialogService<MealAddIngredientDialogState>>();
builder.Services.AddSingleton<PendingDialogService<MealAddStepDialogState>>();

// Register repositories
builder.Services.AddScoped<GroupRepository>();
builder.Services.AddScoped<ShoppingItemRepository>();
builder.Services.AddSingleton<IHistoryRepository, HistoryRepository>();
builder.Services.AddScoped<PurchaseHistoryRepository>();
builder.Services.AddScoped<MealRepository>();
builder.Services.AddScoped<MealIngredientRepository>();
builder.Services.AddScoped<MealStepRepository>();

// Register services
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<MealMergeService>();
builder.Services.AddSingleton<ILocalizer, Localizer>();

// Register command handlers
builder.Services.AddScoped<ICommandHandler, BuyCommandHandler>();
builder.Services.AddScoped<ICommandHandler, ListCommandHandler>();
builder.Services.AddScoped<ICommandHandler, HistoryCommandHandler>();
builder.Services.AddScoped<ICommandHandler, SearchCommandHandler>();
builder.Services.AddScoped<ICommandHandler, LanguageCommandHandler>();
builder.Services.AddScoped<ICommandHandler, MealsCommandHandler>();
builder.Services.AddScoped<ICommandHandler, SettingsCommandHandler>();

// Register dialog message handlers
builder.Services.AddScoped<IDialogMessageHandler, BuyStepHandler>();
builder.Services.AddScoped<IDialogMessageHandler, PriceCaptureStepHandler>();

// Register callback handlers
builder.Services.AddScoped<ICallbackHandler, BuySkipCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ShopDoneCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ShopRemoveCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, PriceSkipCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, LanguageCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ListNextCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, ListPrevCallbackHandler>();
builder.Services.AddScoped<ICallbackHandler, MealCallbackHandler>();

var host = builder.Build();
await host.RunAsync();


