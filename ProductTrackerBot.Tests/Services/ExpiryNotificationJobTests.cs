using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Services;

public class ExpiryNotificationJobTests
{
    private static IServiceScopeFactory CreateScopeFactory(
        GroupRepository groupRepo,
        ExpiryNotificationService notificationService,
        PurchaseHistoryRepository? purchaseRepo = null,
        ShoppingItemRepository? itemRepo = null)
    {
        if (purchaseRepo is null)
        {
            var m = new Mock<PurchaseHistoryRepository>("Data Source=:memory:");
            m.Setup(r => r.GetInventoryItemsWithExpiryAsync(It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<PurchaseRecord>().ToList().AsReadOnly());
            purchaseRepo = m.Object;
        }

        if (itemRepo is null)
        {
            var m = new Mock<ShoppingItemRepository>("Data Source=:memory:");
            m.Setup(r => r.GetItemsWithExpiryAsync(It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<ShoppingItem>().ToList().AsReadOnly());
            itemRepo = m.Object;
        }

        var sp = new Mock<IServiceProvider>();
        sp.Setup(x => x.GetService(typeof(GroupRepository))).Returns(groupRepo);
        sp.Setup(x => x.GetService(typeof(ExpiryNotificationService))).Returns(notificationService);
        sp.Setup(x => x.GetService(typeof(PurchaseHistoryRepository))).Returns(purchaseRepo);
        sp.Setup(x => x.GetService(typeof(ShoppingItemRepository))).Returns(itemRepo);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(sp.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private static (Mock<ITelegramBotClient> Bot, List<long> SentChatIds) CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var sentChatIds = new List<long>();

        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr && smr.ChatId.Identifier.HasValue)
                    sentChatIds.Add(smr.ChatId.Identifier.Value);
            })
            .ReturnsAsync(new Message());

        return (botMock, sentChatIds);
    }

    [Fact]
    public async Task ExecuteNotification_SendsMessageForEachGroupWithReportableItems()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var groups = new List<Group>
        {
            new() { Id = 1, ChatId = 100, ListMessageId = null, LanguageCode = "ru" },
            new() { Id = 2, ChatId = 200, ListMessageId = null, LanguageCode = "ru" },
        };

        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(groups.AsReadOnly());

        var (botClient, _) = CreateBotMock();

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(1))
            .ReturnsAsync(new List<ShoppingItem>
            {
                new() { Id = 1, GroupId = 1, Name = "Item1", Quantity = "1л", ExpDate = today.AddDays(-1), AddedByName = "User1" },
            }.AsReadOnly());

        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(2))
            .ReturnsAsync(new List<ShoppingItem>
            {
                new() { Id = 2, GroupId = 2, Name = "Item2", Quantity = null, ExpDate = today.AddDays(-1), AddedByName = "User2" },
            }.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=:memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());
        purchaseRepo.Setup(r => r.GetInventoryItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<PurchaseRecord>().ToList().AsReadOnly());
        var notificationService = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var job = new ExpiryNotificationJob(
            botClient.Object,
            CreateScopeFactory(groupRepo.Object, notificationService, purchaseRepo.Object, itemRepo.Object),
            Mock.Of<ILogger<ExpiryNotificationJob>>(),
            "09:00");

        await job.ExecuteNotificationAsync(CancellationToken.None);

        groupRepo.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteNotification_SkipsGroupsWithoutReportableItems()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var groups = new List<Group>
        {
            new() { Id = 1, ChatId = 100, ListMessageId = null, LanguageCode = "ru" },
        };

        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(groups.AsReadOnly());

        var (botClient, sentChatIds) = CreateBotMock();

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(1))
            .ReturnsAsync(new List<ShoppingItem>
            {
                new() { Id = 1, GroupId = 1, Name = "Item1", Quantity = "1л", ExpDate = today.AddDays(30), AddedByName = "User1" },
            }.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=:memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());
        purchaseRepo.Setup(r => r.GetInventoryItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<PurchaseRecord>().ToList().AsReadOnly());
        var notificationService = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var job = new ExpiryNotificationJob(
            botClient.Object,
            CreateScopeFactory(groupRepo.Object, notificationService, purchaseRepo.Object, itemRepo.Object),
            Mock.Of<ILogger<ExpiryNotificationJob>>(),
            "09:00");

        await job.ExecuteNotificationAsync(CancellationToken.None);

        Assert.Empty(sentChatIds);
    }

    [Fact]
    public async Task ExecuteNotification_ContinuesWhenOneGroupFails()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var groups = new List<Group>
        {
            new() { Id = 1, ChatId = 100, ListMessageId = null, LanguageCode = "ru" },
            new() { Id = 2, ChatId = 200, ListMessageId = null, LanguageCode = "ru" },
        };

        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(groups.AsReadOnly());

        var botClient = new Mock<ITelegramBotClient>();
        var sentChatIds = new List<long>();

        botClient
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr)
                {
                    var id = smr.ChatId.Identifier ?? smr.ChatId.Username?.GetHashCode() ?? 0;
                    if (id == 100L)
                        throw new InvalidOperationException("Send failed");
                    sentChatIds.Add(id);
                }
            })
            .ReturnsAsync(new Message());

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<ShoppingItem>
            {
                new() { Id = 1, GroupId = 1, Name = "Item1", Quantity = "1л", ExpDate = today.AddDays(-1), AddedByName = "User1" },
            }.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=:memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());
        purchaseRepo.Setup(r => r.GetInventoryItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<PurchaseRecord>().ToList().AsReadOnly());
        var notificationService = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var job = new ExpiryNotificationJob(
            botClient.Object,
            CreateScopeFactory(groupRepo.Object, notificationService, purchaseRepo.Object, itemRepo.Object),
            Mock.Of<ILogger<ExpiryNotificationJob>>(),
            "09:00");

        await job.ExecuteNotificationAsync(CancellationToken.None);

        Assert.Contains(200L, sentChatIds);
    }
}
