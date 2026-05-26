// <copyright file="ExpiryNotificationJob.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Hosted service that runs a daily notification job to send expiry summaries to all registered groups.
/// </summary>
public class ExpiryNotificationJob : IHostedService
{
    private readonly ITelegramBotClient botClient;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<ExpiryNotificationJob> logger;
    private readonly string notifyTimeUtc;
    private readonly int? notifyIntervalMinutes;
    private Task? task;
    private CancellationTokenSource? cts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiryNotificationJob"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="scopeFactory">The scope factory for creating short-lived scopes.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="notifyTimeUtc">The UTC time to fire the job (default "09:00").</param>
    /// <param name="notifyIntervalMinutes">If set, overrides the daily schedule with a repeating interval (dev use).</param>
    public ExpiryNotificationJob(
        ITelegramBotClient botClient,
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiryNotificationJob> logger,
        string notifyTimeUtc = "09:00",
        int? notifyIntervalMinutes = null)
    {
        this.botClient = botClient;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.notifyTimeUtc = notifyTimeUtc;
        this.notifyIntervalMinutes = notifyIntervalMinutes;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("ExpiryNotificationJob starting, notify time: {NotifyTimeUtc}", this.notifyTimeUtc);
        this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.task = this.RunAsync(this.cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.cts?.Cancel();
        await (this.task ?? Task.CompletedTask);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            if (this.notifyIntervalMinutes.HasValue)
            {
                var interval = TimeSpan.FromMinutes(this.notifyIntervalMinutes.Value);
                this.logger.LogInformation("ExpiryNotificationJob running in interval mode: every {Minutes} minutes", this.notifyIntervalMinutes.Value);
                using var timer = new PeriodicTimer(interval);
                while (true)
                {
                    await this.ExecuteNotificationAsync(ct);
                    await timer.WaitForNextTickAsync(ct);
                }
            }
            else
            {
                var (hour, minute) = this.ParseNotifyTime();
                var now = DateTime.UtcNow;
                var nextFireTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);
                if (nextFireTime <= now)
                {
                    nextFireTime = nextFireTime.AddDays(1);
                }

                var initialDelay = nextFireTime - now;
                this.logger.LogInformation("ExpiryNotificationJob next fire in {DelaySeconds} seconds at {NextFireTime} UTC", initialDelay.TotalSeconds, nextFireTime);

                await Task.Delay(initialDelay, ct);

                using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
                while (true)
                {
                    await this.ExecuteNotificationAsync(ct);
                    await timer.WaitForNextTickAsync(ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            this.logger.LogInformation("ExpiryNotificationJob stopped.");
        }
    }

    internal async Task ExecuteNotificationAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("ExpiryNotificationJob executing at {UtcNow}", DateTime.UtcNow);

        using var scope = this.scopeFactory.CreateScope();
        var groupRepository = scope.ServiceProvider.GetRequiredService<GroupRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<ExpiryNotificationService>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var groups = await groupRepository.GetAllAsync();

        var purchaseRepository = scope.ServiceProvider.GetRequiredService<PurchaseHistoryRepository>();
        var itemRepository = scope.ServiceProvider.GetRequiredService<ShoppingItemRepository>();

        foreach (var group in groups)
        {
            try
            {
                var summary = await notificationService.BuildSummaryAsync(group.ChatId, group.Id, today);
                if (summary is not null)
                {
                    var keyboard = await BuildRemoveKeyboardAsync(group.Id, group.ChatId, purchaseRepository, itemRepository, today);
                    await this.botClient.SendMessage(
                        chatId: group.ChatId,
                        text: summary,
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                    this.logger.LogInformation("Sent expiry notification to group {ChatId}", group.ChatId);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to send expiry notification to group {ChatId}", group.ChatId);
            }
        }
    }

    private static async Task<InlineKeyboardMarkup?> BuildRemoveKeyboardAsync(
        int groupId,
        long chatId,
        PurchaseHistoryRepository purchaseRepository,
        ShoppingItemRepository itemRepository,
        DateOnly today)
    {
        var phItems = await purchaseRepository.GetInventoryItemsWithExpiryAsync(groupId);
        var siItems = await itemRepository.GetItemsWithExpiryAsync(groupId);

        var cutoff = today.AddDays(7);

        var entries = siItems
            .Where(i => i.ExpDate!.Value <= cutoff)
            .Select(i => (ExpDate: i.ExpDate!.Value, Label: UseCommandHandler.FormatLabel(i.Name, i.Quantity, i.ExpDate!.Value), Callback: $"use:remove:si:{i.Id}"))
            .Concat(phItems
                .Where(i => i.ExpDate!.Value <= cutoff)
                .Select(i => (ExpDate: i.ExpDate!.Value, Label: UseCommandHandler.FormatLabel(i.ItemName, i.Quantity, i.ExpDate!.Value), Callback: $"use:remove:ph:{i.Id}")))
            .OrderBy(e => e.ExpDate)
            .Take(15)
            .ToList();

        if (entries.Count == 0)
        {
            return null;
        }

        var rows = entries
            .Select(e => new[] { InlineKeyboardButton.WithCallbackData($"🗑 {e.Label}", e.Callback) })
            .ToList();

        return new InlineKeyboardMarkup(rows);
    }

    private (int Hour, int Minute) ParseNotifyTime()
    {
        var parts = this.notifyTimeUtc.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var hour) && int.TryParse(parts[1], out var minute))
        {
            return (hour, minute);
        }

        this.logger.LogWarning("Invalid NOTIFY_TIME_UTC format '{NotifyTimeUtc}', using default 09:00", this.notifyTimeUtc);
        return (9, 0);
    }
}
