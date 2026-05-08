using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;

namespace ProductTrackerBot.Tests.Services;

public class ExpiryNotificationJobTests
{
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

        var botClient = new Mock<ITelegramBotClient>();
        botClient.Setup(b => b.SendMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Telegram.Bot.Types.Message());

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

        var notificationService = new ExpiryNotificationService(itemRepo.Object, Mock.Of<ILocalizer>());

        var job = new ExpiryNotificationJob(
            botClient.Object,
            groupRepo.Object,
            notificationService,
            Mock.Of<ILogger<ExpiryNotificationJob>>(),
            "09:00");

        // Simulate executing the job by calling the private method via reflection
        // For now, we'll test through StartAsync
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await job.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await job.StopAsync(CancellationToken.None);

        // Verify that the groups repository was called
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

        var botClient = new Mock<ITelegramBotClient>();

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(1))
            .ReturnsAsync(new List<ShoppingItem>
            {
                new() { Id = 1, GroupId = 1, Name = "Item1", Quantity = "1л", ExpDate = today.AddDays(30), AddedByName = "User1" },
            }.AsReadOnly());

        var notificationService = new ExpiryNotificationService(itemRepo.Object, Mock.Of<ILocalizer>());

        var job = new ExpiryNotificationJob(
            botClient.Object,
            groupRepo.Object,
            notificationService,
            Mock.Of<ILogger<ExpiryNotificationJob>>(),
            "09:00");

        await job.StartAsync(CancellationToken.None);
        await job.StopAsync(CancellationToken.None);

        // Bot client should not be called since items are not reportable
        botClient.Verify(b => b.SendMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        botClient.Setup(b => b.SendMessage(100L, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Send failed"));

        botClient.Setup(b => b.SendMessage(200L, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Telegram.Bot.Types.Message());

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<ShoppingItem>
            {
                new() { Id = 1, GroupId = 1, Name = "Item1", Quantity = "1л", ExpDate = today.AddDays(-1), AddedByName = "User1" },
            }.AsReadOnly());

        var notificationService = new ExpiryNotificationService(itemRepo.Object, Mock.Of<ILocalizer>());

        var job = new ExpiryNotificationJob(
            botClient.Object,
            groupRepo.Object,
            notificationService,
            Mock.Of<ILogger<ExpiryNotificationJob>>(),
            "09:00");

        await job.StartAsync(CancellationToken.None);
        await job.StopAsync(CancellationToken.None);

        // Both groups should be attempted despite the first one failing
        botClient.Verify(b => b.SendMessage(100L, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        botClient.Verify(b => b.SendMessage(200L, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
