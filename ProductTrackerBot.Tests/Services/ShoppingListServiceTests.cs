using ProductTrackerBot.Localization;
using Moq;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class ShoppingListServiceTests
{
    [Fact]
    public async Task Empty_List_Should_Return_Empty_Message_With_No_Keyboard()
    {
        var groupRepo = CreateGroupRepoMock(chatId: 1, groupId: 10);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(new List<ShoppingItem>().AsReadOnly());
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, localizer.Object);

        var (text, keyboard, group) = await service.BuildListAsync(1);

        Assert.Equal("list.empty", text);
        Assert.Null(keyboard);
        Assert.Equal(10, group.Id);
    }

    // TDD — bug #4: when a category filter matches zero active items (e.g. the last item in that
    // category was bought between render and tap), BuildListAsync must still return a keyboard with an
    // "All" button so the user can clear the filter. Returning a null keyboard strands them.
    [Fact]
    public async Task Filtered_View_With_No_Matches_Should_Offer_All_Button()
    {
        var groupRepo = CreateGroupRepoMock(chatId: 1, groupId: 10);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", AddedByName = "Иван", Category = "Еда" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        itemRepo.Setup(r => r.GetDistinctCategoriesAsync(10)).ReturnsAsync(new[] { "Еда" });
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, localizer.Object);

        // Filter by a category that no active item carries.
        var (_, keyboard, _) = await service.BuildListAsync(1, 1, "Химия");

        Assert.NotNull(keyboard);
        Assert.Contains(
            keyboard!.InlineKeyboard.SelectMany(row => row),
            btn => btn.CallbackData == "list_filter:1:-1:1");
    }

    [Fact]
    public async Task Single_Item_Should_Produce_List_With_One_Row()
    {
        var groupRepo = CreateGroupRepoMock(chatId: 1, groupId: 10);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = "2л", AddedByName = "Иван" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, localizer.Object);

        var (text, keyboard, group) = await service.BuildListAsync(1);

        Assert.Contains("list.header", text);
        Assert.Contains("Молоко (2л)", text);
        Assert.NotNull(keyboard);
        // keyboard has item row + Cancel row
        Assert.Equal(2, keyboard!.InlineKeyboard.Count());

        var row = keyboard.InlineKeyboard.First();
        Assert.Equal(3, row.Count());
        Assert.StartsWith("✓", row.First().Text);
        Assert.Equal("shop:done:1", row.First().CallbackData);
        Assert.Equal("item:edit:1", row.ElementAt(1).CallbackData);
        Assert.Equal("list.remove", row.Last().Text);
        Assert.Equal("shop:remove:1", row.Last().CallbackData);
    }

    [Fact]
    public async Task Multiple_Items_Should_Have_Multiple_Rows()
    {
        var groupRepo = CreateGroupRepoMock(chatId: 1, groupId: 10);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = "2л", AddedByName = "Иван" },
            new() { Id = 2, GroupId = 10, Name = "Хлеб", Quantity = null, AddedByName = "Мария" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, localizer.Object);

        var (text, keyboard, _) = await service.BuildListAsync(1);

        Assert.Contains("Молоко (2л)", text);
        Assert.Contains("Хлеб", text);
        Assert.NotNull(keyboard);
        // keyboard has 2 item rows + Cancel row
        Assert.Equal(3, keyboard!.InlineKeyboard.Count());

        // Verify callback data for both items
        var firstRow = keyboard.InlineKeyboard.First();
        Assert.Equal("shop:done:1", firstRow.First().CallbackData);
        Assert.Equal("item:edit:1", firstRow.ElementAt(1).CallbackData);
        Assert.Equal("shop:remove:1", firstRow.Last().CallbackData);

        var secondRow = keyboard.InlineKeyboard.ElementAt(1);
        Assert.Equal("shop:done:2", secondRow.First().CallbackData);
        Assert.Equal("item:edit:2", secondRow.ElementAt(1).CallbackData);
        Assert.Equal("shop:remove:2", secondRow.Last().CallbackData);
    }

    [Fact]
    public async Task Item_Without_Quantity_Should_Not_Show_Quantity_In_Text()
    {
        var groupRepo = CreateGroupRepoMock(chatId: 1, groupId: 10);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Хлеб", Quantity = null, AddedByName = "Мария" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, localizer.Object);

        var (text, keyboard, _) = await service.BuildListAsync(1);

        Assert.Contains("• Хлеб", text); // Should not have quantity after the name
        // The button text should still show "Хлеб" for the checkmark
        Assert.Contains("✓ Хлеб", keyboard!.InlineKeyboard.First().First().Text);
    }

    [Fact]
    public async Task GetPagedItemsAsync_ValidPageNumber_ReturnsCorrectSlice()
    {
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Item1", Quantity = null, AddedByName = "User1" },
            new() { Id = 2, GroupId = 10, Name = "Item2", Quantity = null, AddedByName = "User1" },
            new() { Id = 3, GroupId = 10, Name = "Item3", Quantity = null, AddedByName = "User1" },
            new() { Id = 4, GroupId = 10, Name = "Item4", Quantity = null, AddedByName = "User1" },
            new() { Id = 5, GroupId = 10, Name = "Item5", Quantity = null, AddedByName = "User1" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(new Mock<GroupRepository>("Data Source=file::memory:?cache=shared").Object, itemRepo.Object, localizer.Object);

        var (pagedItems, totalItems, totalPages, actualPage) = await service.GetPagedItemsAsync(10, pageNumber: 1, pageSize: 2);

        Assert.Equal(2, pagedItems.Count);
        Assert.Equal(1, pagedItems[0].Id);
        Assert.Equal(2, pagedItems[1].Id);
        Assert.Equal(5, totalItems);
        Assert.Equal(3, totalPages);
        Assert.Equal(1, actualPage);
    }

    [Fact]
    public async Task GetPagedItemsAsync_PageExceedsMax_DefaultsToPage1()
    {
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Item1", Quantity = null, AddedByName = "User1" },
            new() { Id = 2, GroupId = 10, Name = "Item2", Quantity = null, AddedByName = "User1" },
            new() { Id = 3, GroupId = 10, Name = "Item3", Quantity = null, AddedByName = "User1" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(new Mock<GroupRepository>("Data Source=file::memory:?cache=shared").Object, itemRepo.Object, localizer.Object);

        var (pagedItems, totalItems, totalPages, actualPage) = await service.GetPagedItemsAsync(10, pageNumber: 99, pageSize: 2);

        Assert.Equal(2, pagedItems.Count);
        Assert.Equal(1, pagedItems[0].Id);
        Assert.Equal(2, pagedItems[1].Id);
        Assert.Equal(3, totalItems);
        Assert.Equal(2, totalPages);
        Assert.Equal(1, actualPage);
    }

    [Fact]
    public async Task GetPagedItemsAsync_EmptyList_ReturnsEmptyItemsAndPageCountOf1()
    {
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(new List<ShoppingItem>().AsReadOnly());
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(new Mock<GroupRepository>("Data Source=file::memory:?cache=shared").Object, itemRepo.Object, localizer.Object);

        var (pagedItems, totalItems, totalPages, actualPage) = await service.GetPagedItemsAsync(10, pageNumber: 1, pageSize: 10);

        Assert.Empty(pagedItems);
        Assert.Equal(0, totalItems);
        Assert.Equal(1, totalPages);
        Assert.Equal(1, actualPage);
    }

    [Fact]
    public async Task GetPagedItemsAsync_ExactlyPageSize_ReturnsSinglePage()
    {
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = Enumerable.Range(1, 10)
            .Select(i => new ShoppingItem { Id = i, GroupId = 10, Name = $"Item{i}", Quantity = null, AddedByName = "User1" })
            .ToList();
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        var localizer = CreateLocalizerMock();

        var service = new ShoppingListService(new Mock<GroupRepository>("Data Source=file::memory:?cache=shared").Object, itemRepo.Object, localizer.Object);

        var (pagedItems, totalItems, totalPages, actualPage) = await service.GetPagedItemsAsync(10, pageNumber: 1, pageSize: 10);

        Assert.Equal(10, pagedItems.Count);
        Assert.Equal(10, totalItems);
        Assert.Equal(1, totalPages);
        Assert.Equal(1, actualPage);
    }

    [Fact]
    public async Task AddItemsAsync_SingleItem_NoComma_AddsOneItem()
    {
        var (service, itemRepo) = CreateServiceWithItemRepo(groupId: 10);
        var added = await service.AddItemsAsync("Молоко", groupId: 10, addedByName: "Ivan");
        Assert.Single(added);
        itemRepo.Verify(r => r.AddAsync(10, "Молоко", null, "Ivan", null, null), Times.Once);
    }

    [Fact]
    public async Task AddItemsAsync_SingleItemWithQuantity_AddsItemWithQty()
    {
        var (service, itemRepo) = CreateServiceWithItemRepo(groupId: 10);
        var added = await service.AddItemsAsync("Молоко 2л", groupId: 10, addedByName: "Ivan");
        Assert.Single(added);
        itemRepo.Verify(r => r.AddAsync(10, "Молоко", "2 л", "Ivan", null, null), Times.Once);
    }

    [Fact]
    public async Task AddItemsAsync_CsvNoQuantities_AddsThreeItems()
    {
        var (service, itemRepo) = CreateServiceWithItemRepo(groupId: 10);
        var added = await service.AddItemsAsync("Молоко, Яйца, Хлеб", groupId: 10, addedByName: "Ivan");
        Assert.Equal(3, added.Count);
        itemRepo.Verify(r => r.AddAsync(10, It.IsAny<string>(), null, "Ivan", null, null), Times.Exactly(3));
    }

    [Fact]
    public async Task AddItemsAsync_CsvWithQuantities_ParsesEachCorrectly()
    {
        var (service, itemRepo) = CreateServiceWithItemRepo(groupId: 10);
        var added = await service.AddItemsAsync("Молоко 2л, Яйца 6, Хлеб", groupId: 10, addedByName: "Ivan");
        Assert.Equal(3, added.Count);
        itemRepo.Verify(r => r.AddAsync(10, "Молоко", "2 л", "Ivan", null, null), Times.Once);
        itemRepo.Verify(r => r.AddAsync(10, "Яйца", "6", "Ivan", null, null), Times.Once);
        itemRepo.Verify(r => r.AddAsync(10, "Хлеб", null, "Ivan", null, null), Times.Once);
    }

    [Fact]
    public async Task AddItemsAsync_WhitespacePadding_TrimsCorrectly()
    {
        var (service, itemRepo) = CreateServiceWithItemRepo(groupId: 10);
        var added = await service.AddItemsAsync("  Молоко  ,  Яйца  ,  Хлеб  ", groupId: 10, addedByName: "Ivan");
        Assert.Equal(3, added.Count);
        itemRepo.Verify(r => r.AddAsync(10, "Молоко", null, "Ivan", null, null), Times.Once);
        itemRepo.Verify(r => r.AddAsync(10, "Яйца", null, "Ivan", null, null), Times.Once);
        itemRepo.Verify(r => r.AddAsync(10, "Хлеб", null, "Ivan", null, null), Times.Once);
    }

    [Fact]
    public async Task AddItemsAsync_MoreThan20Items_OnlyFirst20Added()
    {
        var (service, itemRepo) = CreateServiceWithItemRepo(groupId: 10);
        var csv = string.Join(", ", Enumerable.Range(1, 21).Select(i => $"Item{i}"));
        var added = await service.AddItemsAsync(csv, groupId: 10, addedByName: "Ivan");
        Assert.Equal(20, added.Count);
        itemRepo.Verify(r => r.AddAsync(10, It.IsAny<string>(), It.IsAny<string?>(), "Ivan", null, null), Times.Exactly(20));
    }

    [Fact]
    public async Task AddItemsAsync_EmptySegments_SkipsEmpty()
    {
        var (service, itemRepo) = CreateServiceWithItemRepo(groupId: 10);
        var added = await service.AddItemsAsync("Молоко,,Яйца", groupId: 10, addedByName: "Ivan");
        Assert.Equal(2, added.Count);
        itemRepo.Verify(r => r.AddAsync(10, It.IsAny<string>(), It.IsAny<string?>(), "Ivan", null, null), Times.Exactly(2));
    }

    private static (ShoppingListService Service, Mock<ShoppingItemRepository> ItemRepo) CreateServiceWithItemRepo(int groupId)
    {
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        itemRepo
            .Setup(r => r.AddAsync(groupId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), null))
            .ReturnsAsync((int gid, string name, string? qty, string by, DateOnly? exp, string? category) =>
                new ShoppingItem { Id = 1, GroupId = gid, Name = name, Quantity = qty, AddedByName = by });

        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:?cache=shared");
        var localizer = CreateLocalizerMock();
        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, localizer.Object);
        return (service, itemRepo);
    }

    [Fact]
    public async Task GetPagedItemsAsync_WithCategory_FiltersCaseInsensitively()
    {
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Порошок", AddedByName = "Ivan", Category = "Химия" },
            new() { Id = 2, GroupId = 10, Name = "Молоко", AddedByName = "Ivan", Category = "Еда" },
            new() { Id = 3, GroupId = 10, Name = "Отбеливатель", AddedByName = "Ivan", Category = "химия" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());

        var service = new ShoppingListService(
            new Mock<GroupRepository>("Data Source=file::memory:?cache=shared").Object, itemRepo.Object, CreateLocalizerMock().Object);

        var (pagedItems, totalItems, totalPages, actualPage) = await service.GetPagedItemsAsync(10, pageNumber: 1, pageSize: 10, category: "Химия");

        Assert.Equal(2, totalItems);
        Assert.Equal(1, totalPages);
        Assert.Equal(1, actualPage);
        Assert.Equal(new[] { 1, 3 }, pagedItems.Select(i => i.Id));
    }

    [Fact]
    public async Task BuildListAsync_NoCategorizedItems_OmitsFilterRow()
    {
        var groupRepo = CreateGroupRepoMock(chatId: 1, groupId: 10);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", AddedByName = "Ivan" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        itemRepo.Setup(r => r.GetDistinctCategoriesAsync(10)).ReturnsAsync(Array.Empty<string>());

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, CreateLocalizerMock().Object);

        var (_, keyboard, _) = await service.BuildListAsync(1);

        // Item row + Cancel row only — no filter row
        Assert.Equal(2, keyboard!.InlineKeyboard.Count());
    }

    [Fact]
    public async Task BuildListAsync_WithCategorizedItems_AddsFilterRow()
    {
        var groupRepo = CreateGroupRepoMock(chatId: 1, groupId: 10);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", AddedByName = "Ivan", Category = "Еда" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        itemRepo.Setup(r => r.GetDistinctCategoriesAsync(10)).ReturnsAsync(new List<string> { "Еда" });

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, CreateLocalizerMock().Object);

        var (_, keyboard, _) = await service.BuildListAsync(1);

        // Item row + filter row + Cancel row
        Assert.Equal(3, keyboard!.InlineKeyboard.Count());
        var filterRow = keyboard.InlineKeyboard.ElementAt(1);
        Assert.Single(filterRow);
        Assert.Equal("Еда", filterRow.First().Text);
        Assert.Equal("list_filter:1:0:1", filterRow.First().CallbackData);
    }

    [Fact]
    public async Task BuildListAsync_WithActiveFilter_AddsAllButton()
    {
        var groupRepo = CreateGroupRepoMock(chatId: 1, groupId: 10);
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", AddedByName = "Ivan", Category = "Еда" },
        };
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items.AsReadOnly());
        itemRepo.Setup(r => r.GetDistinctCategoriesAsync(10)).ReturnsAsync(new List<string> { "Еда" });

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object, CreateLocalizerMock().Object);

        var (_, keyboard, _) = await service.BuildListAsync(1, category: "Еда");

        var filterRow = keyboard!.InlineKeyboard.ElementAt(1);
        Assert.Equal(2, filterRow.Count());
        Assert.Equal("list_filter:1:-1:1", filterRow.Last().CallbackData);
    }

    private static Mock<GroupRepository> CreateGroupRepoMock(long chatId, int groupId)
    {
        var mock = new Mock<GroupRepository>("Data Source=file::memory:?cache=shared");
        mock.Setup(r => r.GetOrCreateAsync(chatId))
            .ReturnsAsync(new Group { Id = groupId, ChatId = chatId, ListMessageId = null });
        return mock;
    }

    private static Mock<ILocalizer> CreateLocalizerMock()
    {
        var mock = new Mock<ILocalizer>();
        mock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);
        return mock;
    }
}
