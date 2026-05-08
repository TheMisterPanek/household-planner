// <copyright file="PricesCommandHandlerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Tests.Handlers;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

/// <summary>
/// Tests for PricesCommandHandler.
/// </summary>
public class PricesCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static (Mock<ITelegramBotClient> Bot, List<string> SentTexts) CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var sentTexts = new List<string>();

        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sentTexts.Add(smr.Text);
            })
            .ReturnsAsync(new Message());

        return (botMock, sentTexts);
    }

    private static Mock<GroupRepository> CreateGroupRepoMock()
    {
        var repo = new Mock<GroupRepository>("Data Source=file:test");
        repo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync((long chatId) => new Group { Id = 10, ChatId = chatId });
        return repo;
    }

    private static Mock<ILocalizer> CreateLocalizerMock()
    {
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long chatId, string key) =>
            {
                return key switch
                {
                    "prices.group-only" => "Group only",
                    "prices.usage" => "Usage: /prices <item>",
                    "prices.not-found" => "Not found",
                    "prices.unknown-store" => "Unknown",
                    _ => key,
                };
            });
        return localizer;
    }

    private static Message CreateMessage(string text, long chatId = -100L, long userId = 42L, ChatType chatType = ChatType.Supergroup)
    {
        var json = JsonSerializer.Serialize(new
        {
            message_id = 1,
            from = new { id = userId, first_name = "Alice" },
            chat = new { id = chatId, type = chatType.ToString().ToLower() },
            text,
        }, JsonOpts);
        return DeserializeMessage(json);
    }

    [Fact]
    public async Task HandleAsync_InPrivateChat_SendsGroupOnlyMessage()
    {
        // Arrange
        var (bot, sentTexts) = CreateBotMock();
        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file:test");
        var groupRepo = CreateGroupRepoMock();
        var localizer = CreateLocalizerMock();

        var handler = new PricesCommandHandler(bot.Object, priceLogRepo.Object, groupRepo.Object, localizer.Object);
        var message = CreateMessage("/prices milk", 12345, 42, ChatType.Private);

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert
        Assert.Single(sentTexts);
        Assert.Contains("Group only", sentTexts[0]);
    }

    [Fact]
    public async Task HandleAsync_NoArgument_SendsUsageMessage()
    {
        // Arrange
        var (bot, sentTexts) = CreateBotMock();
        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file:test");
        var groupRepo = CreateGroupRepoMock();
        var localizer = CreateLocalizerMock();

        var handler = new PricesCommandHandler(bot.Object, priceLogRepo.Object, groupRepo.Object, localizer.Object);
        var message = CreateMessage("/prices");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert
        Assert.Single(sentTexts);
        Assert.Contains("Usage", sentTexts[0]);
    }

    [Fact]
    public async Task HandleAsync_NoMatchingData_SendsNotFoundMessage()
    {
        // Arrange
        var (bot, sentTexts) = CreateBotMock();
        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file:test");
        priceLogRepo.Setup(r => r.GetStatsAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((PriceStats?)null);

        var groupRepo = CreateGroupRepoMock();
        var localizer = CreateLocalizerMock();

        var handler = new PricesCommandHandler(bot.Object, priceLogRepo.Object, groupRepo.Object, localizer.Object);
        var message = CreateMessage("/prices milk");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert
        Assert.Single(sentTexts);
        Assert.Contains("Not found", sentTexts[0]);
    }

    [Fact]
    public async Task HandleAsync_WithMatchingData_SendsFormattedStats()
    {
        // Arrange
        var (bot, sentTexts) = CreateBotMock();
        var stats = new PriceStats(
            Min: 2.00m,
            Avg: 3.00m,
            Max: 4.00m,
            Count: 3,
            StoreBreakdown: new List<StoreStats>
            {
                new("Store1", 2.00m, 2.50m, 3.00m, 2),
            });

        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file:test");
        priceLogRepo.Setup(r => r.GetStatsAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(stats);

        var groupRepo = CreateGroupRepoMock();
        var localizer = CreateLocalizerMock();

        var handler = new PricesCommandHandler(bot.Object, priceLogRepo.Object, groupRepo.Object, localizer.Object);
        var message = CreateMessage("/prices Milk");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert
        Assert.Single(sentTexts);
        var response = sentTexts[0];
        Assert.Contains("min 2.00", response);
        Assert.Contains("avg 3.00", response);
        Assert.Contains("max 4.00", response);
        Assert.Contains("3", response);
    }

    [Fact]
    public async Task HandleAsync_SearchIsCaseInsensitive()
    {
        // Arrange
        var (bot, sentTexts) = CreateBotMock();
        var stats = new PriceStats(
            Min: 2.00m,
            Avg: 3.00m,
            Max: 4.00m,
            Count: 2,
            StoreBreakdown: new List<StoreStats>());

        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file:test");
        priceLogRepo.Setup(r => r.GetStatsAsync(10, "milk"))
            .ReturnsAsync(stats);

        var groupRepo = CreateGroupRepoMock();
        var localizer = CreateLocalizerMock();

        var handler = new PricesCommandHandler(bot.Object, priceLogRepo.Object, groupRepo.Object, localizer.Object);
        var message = CreateMessage("/prices milk");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - GetStatsAsync called with lowercase query
        priceLogRepo.Verify(r => r.GetStatsAsync(10, "milk"), Times.Once);
    }
}
