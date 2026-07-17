using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class UndoServiceTests
{
    private static BotActionEntry MakeEntry(BotActionType actionType, string? revertPayload) =>
        new()
        {
            Id = 1,
            ChatId = 100L,
            UserId = 42L,
            UserName = "Alice",
            ActionType = actionType,
            Payload = "{}",
            RecordedAt = DateTime.UtcNow,
            RevertPayload = revertPayload,
        };

    private static Mock<IHistoryRepository> HistoryWithEntry(BotActionEntry? entry)
    {
        var mock = new Mock<IHistoryRepository>();
        mock.Setup(h => h.GetLatestReversibleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        mock.Setup(h => h.MarkRevertedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static Mock<ShoppingItemRepository> ItemRepoThatAdds()
    {
        var mock = new Mock<ShoppingItemRepository>("Data Source=file:test");
        mock.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), null))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 1, Name = "Item", AddedByName = "Undo" });
        return mock;
    }

    [Fact]
    public async Task Returns_Nothing_When_No_Reversible_Entry()
    {
        var historyMock = HistoryWithEntry(null);
        var service = new UndoService(historyMock.Object, new Mock<ShoppingItemRepository>("Data Source=file:test").Object, new Mock<GroupRepository>("Data Source=file:test").Object, Mock.Of<ILogger<UndoService>>());

        var result = await service.UndoLastAsync(100L, 42L, CancellationToken.None);

        Assert.Equal("undo.nothing", result);
    }

    [Fact]
    public async Task Returns_Success_And_Restores_ItemBought()
    {
        var revertPayload = System.Text.Json.JsonSerializer.Serialize(new ItemBoughtRevert(5, "Milk", "1L", 10), BotActionPayloadContext.Default.ItemBoughtRevert);
        var entry = MakeEntry(BotActionType.ItemBought, revertPayload);
        var historyMock = HistoryWithEntry(entry);
        var itemRepo = ItemRepoThatAdds();
        var service = new UndoService(historyMock.Object, itemRepo.Object, new Mock<GroupRepository>("Data Source=file:test").Object, Mock.Of<ILogger<UndoService>>());

        var result = await service.UndoLastAsync(100L, 42L, CancellationToken.None);

        Assert.Equal("undo.success", result);
        itemRepo.Verify(r => r.AddAsync(10, "Milk", "1L", "Undo", null, null), Times.Once);
        historyMock.Verify(h => h.MarkRevertedAsync(1L, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_Success_And_Restores_ItemRemoved()
    {
        var revertPayload = System.Text.Json.JsonSerializer.Serialize(new ItemRemovedRevert("Bread", null, 10), BotActionPayloadContext.Default.ItemRemovedRevert);
        var entry = MakeEntry(BotActionType.ItemRemoved, revertPayload);
        var historyMock = HistoryWithEntry(entry);
        var itemRepo = ItemRepoThatAdds();
        var service = new UndoService(historyMock.Object, itemRepo.Object, new Mock<GroupRepository>("Data Source=file:test").Object, Mock.Of<ILogger<UndoService>>());

        var result = await service.UndoLastAsync(100L, 42L, CancellationToken.None);

        Assert.Equal("undo.success", result);
        itemRepo.Verify(r => r.AddAsync(10, "Bread", null, "Undo", null, null), Times.Once);
    }

    [Fact]
    public async Task Returns_Error_When_Deserialization_Fails()
    {
        var entry = MakeEntry(BotActionType.ItemBought, "{invalid json");
        var historyMock = HistoryWithEntry(entry);
        var service = new UndoService(historyMock.Object, new Mock<ShoppingItemRepository>("Data Source=file:test").Object, new Mock<GroupRepository>("Data Source=file:test").Object, Mock.Of<ILogger<UndoService>>());

        var result = await service.UndoLastAsync(100L, 42L, CancellationToken.None);

        Assert.Equal("undo.error", result);
        historyMock.Verify(h => h.MarkRevertedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_Error_For_Unsupported_Action_Type()
    {
        var revertPayload = System.Text.Json.JsonSerializer.Serialize(new ListArchivedRevert(5), BotActionPayloadContext.Default.ListArchivedRevert);
        var entry = MakeEntry(BotActionType.ListArchived, revertPayload);
        var historyMock = HistoryWithEntry(entry);
        var service = new UndoService(historyMock.Object, new Mock<ShoppingItemRepository>("Data Source=file:test").Object, new Mock<GroupRepository>("Data Source=file:test").Object, Mock.Of<ILogger<UndoService>>());

        var result = await service.UndoLastAsync(100L, 42L, CancellationToken.None);

        Assert.Equal("undo.error", result);
    }

    [Fact]
    public async Task Returns_Error_For_Completely_Unknown_Action_Type()
    {
        var entry = MakeEntry(BotActionType.HistoryViewed, "{\"data\":\"payload\"}");
        var historyMock = HistoryWithEntry(entry);
        var service = new UndoService(historyMock.Object, new Mock<ShoppingItemRepository>("Data Source=file:test").Object, new Mock<GroupRepository>("Data Source=file:test").Object, Mock.Of<ILogger<UndoService>>());

        var result = await service.UndoLastAsync(100L, 42L, CancellationToken.None);

        Assert.Equal("undo.error", result);
    }
}
