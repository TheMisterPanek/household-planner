using Moq;
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

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object);

        var (text, keyboard, group) = await service.BuildListAsync(1);

        Assert.Equal("Список покупок пуст", text);
        Assert.Null(keyboard);
        Assert.Equal(10, group.Id);
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

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object);

        var (text, keyboard, group) = await service.BuildListAsync(1);

        Assert.Contains("🛒 Список покупок:", text);
        Assert.Contains("Молоко 2л", text);
        Assert.NotNull(keyboard);
        Assert.Single(keyboard!.InlineKeyboard);

        var row = keyboard.InlineKeyboard.First();
        Assert.Equal(2, row.Count());
        Assert.StartsWith("✓", row.First().Text);
        Assert.Equal("shop:done:1", row.First().CallbackData);
        Assert.Equal("✗ Убрать", row.Last().Text);
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

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object);

        var (text, keyboard, _) = await service.BuildListAsync(1);

        Assert.Contains("Молоко 2л", text);
        Assert.Contains("Хлеб", text);
        Assert.NotNull(keyboard);
        Assert.Equal(2, keyboard!.InlineKeyboard.Count());

        // Verify callback data for both items
        var firstRow = keyboard.InlineKeyboard.First();
        Assert.Equal("shop:done:1", firstRow.First().CallbackData);
        Assert.Equal("shop:remove:1", firstRow.Last().CallbackData);

        var secondRow = keyboard.InlineKeyboard.Last();
        Assert.Equal("shop:done:2", secondRow.First().CallbackData);
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

        var service = new ShoppingListService(groupRepo.Object, itemRepo.Object);

        var (text, keyboard, _) = await service.BuildListAsync(1);

        Assert.Contains("• Хлеб", text); // Should not have quantity after the name
        // The button text should still show "Хлеб" for the checkmark
        Assert.Contains("✓ Хлеб", keyboard!.InlineKeyboard.First().First().Text);
    }

    private static Mock<GroupRepository> CreateGroupRepoMock(long chatId, int groupId)
    {
        var mock = new Mock<GroupRepository>("Data Source=file::memory:?cache=shared");
        mock.Setup(r => r.GetOrCreateAsync(chatId))
            .ReturnsAsync(new Group { Id = groupId, ChatId = chatId, ListMessageId = null });
        return mock;
    }
}
