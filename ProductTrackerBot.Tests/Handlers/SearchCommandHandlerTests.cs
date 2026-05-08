using ProductTrackerBot.Localization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class SearchCommandHandlerTests
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

    private static Mock<PurchaseHistoryRepository> CreatePurchaseRepoMock()
    {
        return new Mock<PurchaseHistoryRepository>("Data Source=file:test");
    }

    private static Message CreateMessage(string text, long chatId = -100L, long userId = 42L)
    {
        var json = JsonSerializer.Serialize(new
        {
            message_id = 1,
            from = new { id = userId, first_name = "Alice" },
            chat = new { id = chatId, type = "supergroup" },
            text,
        }, JsonOpts);
        return DeserializeMessage(json);
    }

    private static SearchCommandHandler CreateHandler(
        ITelegramBotClient bot,
        PurchaseHistoryRepository purchaseRepo,
        GroupRepository groupRepo)
    {
        return new SearchCommandHandler(
            bot,
            purchaseRepo,
            groupRepo,
            Mock.Of<ILogger<SearchCommandHandler>>());
    }

    private static PurchaseRecord Record(string itemName, decimal? price, string? store, DateTime purchasedAt, int groupId = 10)
    {
        return new PurchaseRecord
        {
            Id = 1,
            GroupId = groupId,
            ItemName = itemName,
            Price = price,
            StoreName = store,
            PurchasedAt = purchasedAt,
            BoughtByName = "Alice",
        };
    }

    [Fact]
    public async Task No_Argument_Sends_Usage_Message()
    {
        var (bot, sentTexts) = CreateBotMock();
        var handler = CreateHandler(
            bot.Object,
            CreatePurchaseRepoMock().Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Equal("Usage: /search <item name>", sentTexts[0]);
    }

    [Fact]
    public async Task No_Argument_When_Only_Slash_Also_Sends_Usage()
    {
        var (bot, sentTexts) = CreateBotMock();
        var handler = CreateHandler(
            bot.Object,
            CreatePurchaseRepoMock().Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search "), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Equal("Usage: /search <item name>", sentTexts[0]);
    }

    [Fact]
    public async Task Empty_Results_Sends_No_Results_Message()
    {
        var (bot, sentTexts) = CreateBotMock();
        var purchaseRepo = CreatePurchaseRepoMock();
        purchaseRepo
            .Setup(r => r.SearchAsync(10, "milk"))
            .ReturnsAsync(Array.Empty<PurchaseRecord>());

        var handler = CreateHandler(
            bot.Object,
            purchaseRepo.Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search milk"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Equal("No purchase history found for 'milk'", sentTexts[0]);
    }

    [Fact]
    public async Task With_Results_Formats_Item_Groups()
    {
        var (bot, sentTexts) = CreateBotMock();
        var purchaseRepo = CreatePurchaseRepoMock();
        var now = DateTime.UtcNow;
        purchaseRepo
            .Setup(r => r.SearchAsync(10, "milk"))
            .ReturnsAsync(new[]
            {
                Record("Milk", 89.90m, "Magnit", now.AddDays(-1)),
                Record("Milk", 95.00m, "Auchan", now),
            });

        var handler = CreateHandler(
            bot.Object,
            purchaseRepo.Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search milk"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Contains("📊 Search results for 'milk'", sentTexts[0]);
        Assert.Contains("• Milk", sentTexts[0]);
    }

    [Fact]
    public async Task Price_Increase_Shows_Positive_Delta()
    {
        var (bot, sentTexts) = CreateBotMock();
        var purchaseRepo = CreatePurchaseRepoMock();
        var now = DateTime.UtcNow;
        purchaseRepo
            .Setup(r => r.SearchAsync(10, "milk"))
            .ReturnsAsync(new[]
            {
                Record("Milk", 100m, null, now.AddDays(-1)),
                Record("Milk", 150m, null, now),
            });

        var handler = CreateHandler(
            bot.Object,
            purchaseRepo.Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search milk"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Contains("(+50.0%)", sentTexts[0]);
    }

    [Fact]
    public async Task Price_Decrease_Shows_Negative_Delta()
    {
        var (bot, sentTexts) = CreateBotMock();
        var purchaseRepo = CreatePurchaseRepoMock();
        var now = DateTime.UtcNow;
        purchaseRepo
            .Setup(r => r.SearchAsync(10, "milk"))
            .ReturnsAsync(new[]
            {
                Record("Milk", 200m, null, now.AddDays(-1)),
                Record("Milk", 150m, null, now),
            });

        var handler = CreateHandler(
            bot.Object,
            purchaseRepo.Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search milk"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Contains("(-25.0%)", sentTexts[0]);
    }

    [Fact]
    public async Task Single_Purchase_Shows_No_Delta()
    {
        var (bot, sentTexts) = CreateBotMock();
        var purchaseRepo = CreatePurchaseRepoMock();
        var now = DateTime.UtcNow;
        purchaseRepo
            .Setup(r => r.SearchAsync(10, "milk"))
            .ReturnsAsync(new[]
            {
                Record("Milk", 100m, null, now),
            });

        var handler = CreateHandler(
            bot.Object,
            purchaseRepo.Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search milk"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Contains("— 100.00", sentTexts[0]);
        Assert.DoesNotContain("%", sentTexts[0]);
    }

    [Fact]
    public async Task Latest_Without_Price_Shows_No_Delta()
    {
        var (bot, sentTexts) = CreateBotMock();
        var purchaseRepo = CreatePurchaseRepoMock();
        var now = DateTime.UtcNow;
        purchaseRepo
            .Setup(r => r.SearchAsync(10, "milk"))
            .ReturnsAsync(new[]
            {
                Record("Milk", 100m, "Magnit", now.AddDays(-1)),
                Record("Milk", null, "Auchan", now),
            });

        var handler = CreateHandler(
            bot.Object,
            purchaseRepo.Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search milk"), CancellationToken.None);

        Assert.Single(sentTexts);
        // Latest has no price, so it shows store only
        Assert.Contains("• Milk at Auchan", sentTexts[0]);
        Assert.DoesNotContain("%", sentTexts[0]);
    }

    [Fact]
    public async Task No_Argument_Sends_Usage_When_Unrelated_Command_Text()
    {
        var (bot, sentTexts) = CreateBotMock();
        var handler = CreateHandler(
            bot.Object,
            CreatePurchaseRepoMock().Object,
            CreateGroupRepoMock().Object);

        await handler.HandleAsync(CreateMessage("/search"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Equal("Usage: /search <item name>", sentTexts[0]);
    }
}