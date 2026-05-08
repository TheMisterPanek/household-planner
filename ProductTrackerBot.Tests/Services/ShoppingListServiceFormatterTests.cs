using Moq;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class ShoppingListServiceFormatterTests
{
    private ShoppingListService CreateService(IReadOnlyList<ShoppingItem>? items = null)
    {
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:?cache=shared");
        groupRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync(new Group { Id = 10, ChatId = 1, ListMessageId = null });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:?cache=shared");
        itemRepo.Setup(r => r.GetAllAsync(10))
            .ReturnsAsync(items ?? new List<ShoppingItem>());

        var localizer = CreateLocalizerMock();
        return new ShoppingListService(groupRepo.Object, itemRepo.Object, localizer.Object);
    }

    private static Mock<ILocalizer> CreateLocalizerMock()
    {
        var mock = new Mock<ILocalizer>();
        mock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);
        return mock;
    }

    [Fact]
    public async Task Formatter_SinglePageNoPagination_NoNavigationButtons()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Item1", Quantity = null, AddedByName = "User1" },
        };
        var service = CreateService(items);

        var (text, keyboard, _) = await service.BuildListAsync(1, pageNumber: 1);

        Assert.NotNull(keyboard);
        var buttonRows = keyboard!.InlineKeyboard.ToList();
        // Should have 1 row for the item (2 buttons: done + remove)
        // Should NOT have pagination buttons
        Assert.Single(buttonRows);
        Assert.DoesNotContain("Previous", text);
        Assert.DoesNotContain("Next", text);
    }

    [Fact]
    public async Task Formatter_FirstPageMultiplePages_ShowsNextButton()
    {
        var items = Enumerable.Range(1, 15)
            .Select(i => new ShoppingItem { Id = i, GroupId = 10, Name = $"Item{i}", Quantity = null, AddedByName = "User1" })
            .ToList();
        var service = CreateService(items);

        var (text, keyboard, _) = await service.BuildListAsync(1, pageNumber: 1);

        Assert.NotNull(keyboard);
        Assert.Contains("Next", text);
        Assert.DoesNotContain("Previous", text);
    }

    [Fact]
    public async Task Formatter_MiddlePageMultiplePages_ShowsBothButtons()
    {
        var items = Enumerable.Range(1, 25)
            .Select(i => new ShoppingItem { Id = i, GroupId = 10, Name = $"Item{i}", Quantity = null, AddedByName = "User1" })
            .ToList();
        var service = CreateService(items);

        var (text, keyboard, _) = await service.BuildListAsync(1, pageNumber: 2);

        Assert.NotNull(keyboard);
        Assert.Contains("Previous", text);
        Assert.Contains("Next", text);
    }

    [Fact]
    public async Task Formatter_LastPageMultiplePages_ShowsPreviousButton()
    {
        var items = Enumerable.Range(1, 15)
            .Select(i => new ShoppingItem { Id = i, GroupId = 10, Name = $"Item{i}", Quantity = null, AddedByName = "User1" })
            .ToList();
        var service = CreateService(items);

        var (text, keyboard, _) = await service.BuildListAsync(1, pageNumber: 2);

        Assert.NotNull(keyboard);
        Assert.Contains("Previous", text);
        Assert.DoesNotContain("Next", text);
    }

    [Fact]
    public async Task Formatter_PageFooter_DisplaysCorrectPageInfo()
    {
        var items = Enumerable.Range(1, 25)
            .Select(i => new ShoppingItem { Id = i, GroupId = 10, Name = $"Item{i}", Quantity = null, AddedByName = "User1" })
            .ToList();
        var service = CreateService(items);

        var (text, _, _) = await service.BuildListAsync(1, pageNumber: 2);

        Assert.Contains("Page 2 of 3", text);
        Assert.Contains("(25 total items)", text);
    }

    [Fact]
    public async Task Formatter_CallbackData_ContainsValidGroupIdAndPageNumber()
    {
        var items = Enumerable.Range(1, 15)
            .Select(i => new ShoppingItem { Id = i, GroupId = 10, Name = $"Item{i}", Quantity = null, AddedByName = "User1" })
            .ToList();
        var service = CreateService(items);

        var (_, keyboard, _) = await service.BuildListAsync(chatId: 1, pageNumber: 1);

        Assert.NotNull(keyboard);
        var paginationRow = keyboard!.InlineKeyboard.Last();
        var nextButton = paginationRow.First(b => b.Text.Contains("Next"));
        Assert.NotNull(nextButton.CallbackData);
        // Format should be list_next:chatId:pageNumber
        Assert.StartsWith("list_next:1:", nextButton.CallbackData);
    }
}
