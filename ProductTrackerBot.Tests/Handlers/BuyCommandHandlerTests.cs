using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class BuyCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Message GroupBuyMessage(string args) =>
        DeserializeMessage($"{{\"message_id\":1,\"from\":{{\"id\":42,\"first_name\":\"Alice\"}},\"chat\":{{\"id\":-100,\"type\":\"supergroup\"}},\"text\":\"/buy {args}\"}}");

    private static Message GroupBuyMessageNoArgs() =>
        DeserializeMessage("{\"message_id\":1,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"/buy\"}");

    private (BuyCommandHandler Handler, Mock<ITelegramBotClient> Bot, Mock<ShoppingListService> ListService, Mock<IHistoryRepository> History) CreateHandler()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });

        var listService = new Mock<ShoppingListService>(
            new Mock<GroupRepository>("Data Source=file::memory:").Object,
            new Mock<ShoppingItemRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());

        var history = new Mock<IHistoryRepository>();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BuyCommandHandler(
            bot.Object,
            groupRepo.Object,
            new PendingDialogService<BuyDialogState>(),
            new PendingAddService(),
            localizer.Object,
            listService.Object,
            history.Object,
            Mock.Of<ILogger<BuyCommandHandler>>());

        return (handler, bot, listService, history);
    }

    [Fact]
    public async Task Single_Item_Without_Comma_Uses_Review_Path()
    {
        var (handler, bot, listService, _) = CreateHandler();

        await handler.HandleAsync(GroupBuyMessage("Молоко 2л"), CancellationToken.None);

        // Review message sent (with inline keyboard)
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        // Bulk add NOT called
        listService.Verify(s => s.AddItemsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Comma_In_Args_Calls_AddItemsAsync()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = null, AddedByName = "Alice" },
            new() { Id = 2, GroupId = 10, Name = "Яйца", Quantity = null, AddedByName = "Alice" },
        };

        var (handler, bot, listService, _) = CreateHandler();
        listService.Setup(s => s.AddItemsAsync("Молоко, Яйца", 10, "Alice"))
            .ReturnsAsync(items.AsReadOnly());

        await handler.HandleAsync(GroupBuyMessage("Молоко, Яйца"), CancellationToken.None);

        listService.Verify(s => s.AddItemsAsync("Молоко, Яйца", 10, "Alice"), Times.Once);
    }

    [Fact]
    public async Task Bulk_Add_Records_History_Per_Item()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = "2л", AddedByName = "Alice" },
            new() { Id = 2, GroupId = 10, Name = "Яйца", Quantity = null, AddedByName = "Alice" },
        };

        var (handler, _, listService, history) = CreateHandler();
        listService.Setup(s => s.AddItemsAsync(It.IsAny<string>(), 10, "Alice"))
            .ReturnsAsync(items.AsReadOnly());

        await handler.HandleAsync(GroupBuyMessage("Молоко 2л, Яйца"), CancellationToken.None);

        history.Verify(h => h.RecordAsync(
            -100L, 42L, "Alice", BotActionType.ItemAdded,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Bulk_Add_Sends_Confirmation_With_BulkAdded_Key()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = null, AddedByName = "Alice" },
            new() { Id = 2, GroupId = 10, Name = "Яйца", Quantity = null, AddedByName = "Alice" },
        };

        var (handler, bot, listService, _) = CreateHandler();
        listService.Setup(s => s.AddItemsAsync(It.IsAny<string>(), 10, "Alice"))
            .ReturnsAsync(items.AsReadOnly());

        string? sentText = null;
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sentText = smr.Text;
            })
            .ReturnsAsync(new Message());

        await handler.HandleAsync(GroupBuyMessage("Молоко, Яйца"), CancellationToken.None);

        Assert.NotNull(sentText);
        Assert.Contains("shop.bulk-added", sentText);
    }

    [Fact]
    public async Task Bulk_Add_History_Failure_Does_Not_Suppress_Confirmation()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Хлеб", Quantity = null, AddedByName = "Alice" },
        };

        var (handler, bot, listService, history) = CreateHandler();
        listService.Setup(s => s.AddItemsAsync(It.IsAny<string>(), 10, "Alice"))
            .ReturnsAsync(items.AsReadOnly());
        history.Setup(h => h.RecordAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("db failure"));

        // Should not throw; confirmation still sent
        await handler.HandleAsync(GroupBuyMessage("Хлеб, Масло"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_Args_Starts_Dialog()
    {
        var (handler, bot, _, _) = CreateHandler();

        await handler.HandleAsync(GroupBuyMessageNoArgs(), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
