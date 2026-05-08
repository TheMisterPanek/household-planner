// <copyright file="ExpiryNotificationJob.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Services;

using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductTrackerBot.Repositories;
using Telegram.Bot;

/// <summary>
/// Hosted service that runs a daily notification job to send expiry summaries to all registered groups.
/// </summary>
public class ExpiryNotificationJob : IHostedService
{
    private readonly ITelegramBotClient botClient;
    private readonly GroupRepository groupRepository;
    private readonly ExpiryNotificationService notificationService;
    private readonly ILogger<ExpiryNotificationJob> logger;
    private readonly string notifyTimeUtc;
    private Timer? timer;
    private CancellationTokenSource? timerCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiryNotificationJob"/> class.
    /// </summary>
    /// <param name="botClient">The Telegram bot client.</param>
    /// <param name="groupRepository">The group repository.</param>
    /// <param name="notificationService">The expiry notification service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="notifyTimeUtc">The UTC time to fire the job (default "09:00").</param>
    public ExpiryNotificationJob(
        ITelegramBotClient botClient,
        GroupRepository groupRepository,
        ExpiryNotificationService notificationService,
        ILogger<ExpiryNotificationJob> logger,
        string notifyTimeUtc = "09:00")
    {
        this.botClient = botClient;
        this.groupRepository = groupRepository;
        this.notificationService = notificationService;
        this.logger = logger;
        this.notifyTimeUtc = notifyTimeUtc;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("ExpiryNotificationJob starting, notify time: {NotifyTimeUtc}", this.notifyTimeUtc);

        var (hour, minute) = this.ParseNotifyTime();
        var now = DateTime.UtcNow;
        var nextFireTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);

        if (nextFireTime <= now)
        {
            nextFireTime = nextFireTime.AddDays(1);
        }

        var initialDelay = nextFireTime - now;
        this.logger.LogInformation("ExpiryNotificationJob next fire in {DelaySeconds} seconds at {NextFireTime} UTC", initialDelay.TotalSeconds, nextFireTime);

        this.timerCts = new CancellationTokenSource();
        this.timer = new Timer(
            this.TimerCallback,
            null,
            initialDelay,
            TimeSpan.FromHours(24));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.timer?.Dispose();
        this.timerCts?.Cancel();
        this.timerCts?.Dispose();
        return Task.CompletedTask;
    }

    private void TimerCallback(object? state)
    {
        try
        {
            _ = this.ExecuteNotificationAsync(this.timerCts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "ExpiryNotificationJob execution failed");
        }
    }

    private async Task ExecuteNotificationAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("ExpiryNotificationJob executing at {UtcNow}", DateTime.UtcNow);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var groups = await this.groupRepository.GetAllAsync();

        foreach (var group in groups)
        {
            try
            {
                var summary = await this.notificationService.BuildSummaryAsync(group.ChatId, group.Id, today);
                if (summary is not null)
                {
                    await this.botClient.SendMessage(
                        chatId: group.ChatId,
                        text: summary,
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
