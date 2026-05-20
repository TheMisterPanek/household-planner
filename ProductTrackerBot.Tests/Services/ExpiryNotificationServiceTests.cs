using Moq;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class ExpiryNotificationServiceTests
{
    private static Mock<ILocalizer> KeyNameLocalizer()
    {
        var mock = new Mock<ILocalizer>();
        mock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);
        return mock;
    }

    [Fact]
    public async Task BuildSummaryAsync_WithExpiredItem_ContainsExpiredSectionKey()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Milk", Quantity = "2л", ExpDate = today.AddDays(-1), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("notify.section-expired", result);
        Assert.DoesNotContain("Просроченные", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithNoExpiryItems_ReturnsNull()
    {
        var today = new DateOnly(2026, 5, 8);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithItemsInAllBuckets_ContainsAllSectionKeys()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Expired", Quantity = null, ExpDate = today.AddDays(-1), AddedByName = "User1" },
            new() { Id = 2, GroupId = 10, Name = "Today", Quantity = "1л", ExpDate = today, AddedByName = "User1" },
            new() { Id = 3, GroupId = 10, Name = "Soon", Quantity = "2л", ExpDate = today.AddDays(1), AddedByName = "User1" },
            new() { Id = 4, GroupId = 10, Name = "Week", Quantity = null, ExpDate = today.AddDays(6), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("notify.section-expired", result);
        Assert.Contains("notify.section-today", result);
        Assert.Contains("notify.section-soon", result);
        Assert.Contains("notify.section-week", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithTodayItems_IncludesYellowSection()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Yogurt", Quantity = "500г", ExpDate = today, AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("notify.section-today", result);
        Assert.Contains("Yogurt 500г (08.05.2026)", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithSoonItems_IncludesOrangeSection()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Cheese", Quantity = "200г", ExpDate = today.AddDays(2), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("notify.section-soon", result);
        Assert.Contains("Cheese 200г (10.05.2026)", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithWeekItems_IncludesCalendarSection()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Bread", Quantity = null, ExpDate = today.AddDays(5), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("notify.section-week", result);
        Assert.Contains("Bread (13.05.2026)", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithNoReportableItems_ReturnsNull()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Far future", Quantity = "1л", ExpDate = today.AddDays(30), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithNoItems_ReturnsNull()
    {
        var today = new DateOnly(2026, 5, 8);

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildSummaryAsync_ItemWithoutQuantity_ShowsOnlyName()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Eggs", Quantity = null, ExpDate = today.AddDays(-1), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("• Eggs\n", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_IncludesBoughtItemsWithExpiry()
    {
        var today = new DateOnly(2026, 5, 8);
        var plannedItems = new List<ShoppingItem>();

        var boughtItems = new List<(string, string?, DateOnly)>
        {
            ("Milk", "2л", today.AddDays(-1)),
            ("Yogurt", "500г", today),
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(plannedItems.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(boughtItems.AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("notify.section-expired", result);
        Assert.Contains("Milk 2л", result);
        Assert.Contains("notify.section-today", result);
        Assert.Contains("Yogurt 500г", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_MergesBoughtAndPlannedItemsCorrectly()
    {
        var today = new DateOnly(2026, 5, 8);
        var plannedItems = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Planed Milk", Quantity = "1л", ExpDate = today.AddDays(1), AddedByName = "User1" },
        };

        var boughtItems = new List<(string, string?, DateOnly)>
        {
            ("Bought Yogurt", "500г", today.AddDays(2)),
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(plannedItems.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(boughtItems.AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, KeyNameLocalizer().Object);

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("Planed Milk", result);
        Assert.Contains("Bought Yogurt", result);
    }
}
