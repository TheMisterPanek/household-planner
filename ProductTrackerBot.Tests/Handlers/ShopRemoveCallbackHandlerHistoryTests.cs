using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class ShopRemoveCallbackHandlerHistoryTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery DeserializeCallbackQuery(string json) =>
        JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        botMock.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        botMock.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        return botMock;
    }

    private static CallbackQuery RemoveCallbackQuery() =>
        DeserializeCallbackQuery(
            "{\"id\":\"cb1\",\"from\":{\"id\":42,\"first_name\":\"Alice\"}," +
            "\"message\":{\"message_id\":10,\"chat\":{\"id\":-100}}," +
            "\"data\":\"shop:remove:7\"}");

    [Fact]
    public async Task Calls_RecordAsync_With_ItemRemoved()
    {
        var bot = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.DeleteAsync(7)).Returns(Task.CompletedTask);
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        var listService = new ShoppingListService(groupRepo.Object, itemRepo.Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object, Mock.Of<ILocalizer>());

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ShopRemoveCallbackHandler(
            bot.Object, itemRepo.Object, listService, groupRepo.Object, historyMock.Object,
            Mock.Of<ILogger<ShopRemoveCallbackHandler>>());

        await handler.HandleAsync(RemoveCallbackQuery(), CancellationToken.None);

        historyMock.Verify(
            h => h.RecordAsync(-100L, 42L, "Alice", BotActionType.ItemRemoved, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Swallows_RecordAsync_Failure()
    {
        var bot = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.DeleteAsync(7)).Returns(Task.CompletedTask);
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        var listService = new ShoppingListService(groupRepo.Object, itemRepo.Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object, Mock.Of<ILocalizer>());

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var handler = new ShopRemoveCallbackHandler(
            bot.Object, itemRepo.Object, listService, groupRepo.Object, historyMock.Object,
            Mock.Of<ILogger<ShopRemoveCallbackHandler>>());

        await handler.HandleAsync(RemoveCallbackQuery(), CancellationToken.None);
    }

    [Fact]
    public async Task Passes_ItemRemovedRevert_Payload_With_Item_Details()
    {
        var bot = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var item = new ShoppingItem { Id = 7, GroupId = 10, Name = "Хлеб", Quantity = "1шт", AddedByName = "Alice" };
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(item);
        itemRepo.Setup(r => r.DeleteAsync(7)).Returns(Task.CompletedTask);
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        var listService = new ShoppingListService(groupRepo.Object, itemRepo.Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object, Mock.Of<ILocalizer>());

        string? capturedRevert = null;
        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<long, long, string, BotActionType, string, string?, CancellationToken>(
                (_, _, _, _, _, revert, _) => capturedRevert = revert)
            .Returns(Task.CompletedTask);

        var handler = new ShopRemoveCallbackHandler(
            bot.Object, itemRepo.Object, listService, groupRepo.Object, historyMock.Object,
            Mock.Of<ILogger<ShopRemoveCallbackHandler>>());

        await handler.HandleAsync(RemoveCallbackQuery(), CancellationToken.None);

        Assert.NotNull(capturedRevert);
        var revert = System.Text.Json.JsonSerializer.Deserialize<ItemRemovedRevert>(capturedRevert!, BotActionPayloadContext.Default.ItemRemovedRevert);
        Assert.NotNull(revert);
        Assert.Equal("Хлеб", revert!.ItemName);
        Assert.Equal("1шт", revert.Quantity);
        Assert.Equal(10, revert.ListId);
    }
}
