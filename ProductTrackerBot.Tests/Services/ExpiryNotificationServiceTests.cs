using Moq;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class ExpiryNotificationServiceTests
{
    [Fact]
    public async Task BuildSummaryAsync_WithExpiredItems_IncludesRedSection()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = "2л", ExpDate = today.AddDays(-1), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("🔴 Просроченные:", result);
        Assert.Contains("Молоко 2л", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithTodayItems_IncludesYellowSection()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Йогурт", Quantity = "500г", ExpDate = today, AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("🟡 Истекает сегодня:", result);
        Assert.Contains("Йогурт 500г (08.05.2026)", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithSoonItems_IncludesOrangeSection()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Сыр", Quantity = "200г", ExpDate = today.AddDays(2), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("🟠 Истекает скоро (до 3 дней):", result);
        Assert.Contains("Сыр 200г (10.05.2026)", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithWeekItems_IncludesCalendarSection()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Хлеб", Quantity = null, ExpDate = today.AddDays(5), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("📅 Истекает на этой неделе (4–7 дней):", result);
        Assert.Contains("Хлеб (13.05.2026)", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithMultipleSections_IncludesAllSections()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Просроченное", Quantity = null, ExpDate = today.AddDays(-1), AddedByName = "User1" },
            new() { Id = 2, GroupId = 10, Name = "Сегодня", Quantity = "1л", ExpDate = today, AddedByName = "User1" },
            new() { Id = 3, GroupId = 10, Name = "Скоро", Quantity = "2л", ExpDate = today.AddDays(1), AddedByName = "User1" },
            new() { Id = 4, GroupId = 10, Name = "На неделе", Quantity = null, ExpDate = today.AddDays(6), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("🔴 Просроченные:", result);
        Assert.Contains("🟡 Истекает сегодня:", result);
        Assert.Contains("🟠 Истекает скоро (до 3 дней):", result);
        Assert.Contains("📅 Истекает на этой неделе (4–7 дней):", result);
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

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildSummaryAsync_WithNoItems_ReturnsNull()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>();

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildSummaryAsync_ItemWithoutQuantity_ShowsOnlyName()
    {
        var today = new DateOnly(2026, 5, 8);
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Яйца", Quantity = null, ExpDate = today.AddDays(-1), AddedByName = "User1" },
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(items.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(new List<(string, string?, DateOnly)>().AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("• Яйца\n", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_IncludesBoughtItemsWithExpiry()
    {
        var today = new DateOnly(2026, 5, 8);
        var plannedItems = new List<ShoppingItem>();

        var boughtItems = new List<(string, string?, DateOnly)>
        {
            ("Молоко", "2л", today.AddDays(-1)),
            ("Йогурт", "500г", today),
        };

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(plannedItems.AsReadOnly());

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetItemsWithExpiryAsync(10))
            .ReturnsAsync(boughtItems.AsReadOnly());

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("🔴 Просроченные:", result);
        Assert.Contains("Молоко 2л", result);
        Assert.Contains("🟡 Истекает сегодня:", result);
        Assert.Contains("Йогурт 500г", result);
    }

    [Fact]
    public async Task BuildSummaryAsync_MergesBoughtAndPlannedItemsCorrectly()
    {
        var today = new DateOnly(2026, 5, 8);
        var plannedItems = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Плaned Milk", Quantity = "1л", ExpDate = today.AddDays(1), AddedByName = "User1" },
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

        var service = new ExpiryNotificationService(itemRepo.Object, purchaseRepo.Object, Mock.Of<ILocalizer>());

        var result = await service.BuildSummaryAsync(1, 10, today);

        Assert.NotNull(result);
        Assert.Contains("Planed Milk", result);
        Assert.Contains("Bought Yogurt", result);
    }
}
