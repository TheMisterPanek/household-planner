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

    private (BuyCommandHandler Handler, Mock<ITelegramBotClient> Bot, Mock<ShoppingListService> ListService, Mock<IHistoryRepository> History, Mock<TagCaptureService> TagCaptureService, Mock<ShoppingItemRepository> ItemRepository) CreateHandler()
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
            new Mock<TagRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());

        var history = new Mock<IHistoryRepository>();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var tagRepo = new Mock<TagRepository>("Data Source=file::memory:");
        tagRepo.Setup(r => r.GetTopTagsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());
        var tagCaptureServiceMock = new Mock<TagCaptureService>(
            bot.Object, new PendingDialogService<TagCaptureDialogState>(), tagRepo.Object, localizer.Object);
        tagCaptureServiceMock.Setup(s => s.StartTagCaptureAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()))
            .ReturnsAsync((int groupId, string name, string? quantity, string addedByName, DateOnly? _) =>
                new ShoppingItem { Id = 99, GroupId = groupId, Name = name, Quantity = quantity, AddedByName = addedByName });

        var buyAddService = new BuyAddService(
            bot.Object, itemRepo.Object, history.Object, tagCaptureServiceMock.Object, localizer.Object,
            Mock.Of<ILogger<BuyAddService>>());

        var handler = new BuyCommandHandler(
            bot.Object,
            groupRepo.Object,
            new PendingDialogService<BuyDialogState>(),
            new PendingAddService(),
            localizer.Object,
            listService.Object,
            history.Object,
            tagCaptureServiceMock.Object,
            buyAddService,
            Mock.Of<ILogger<BuyCommandHandler>>());

        return (handler, bot, listService, history, tagCaptureServiceMock, itemRepo);
    }

    [Fact]
    public async Task Single_Item_Without_Comma_Uses_Review_Path()
    {
        var (handler, bot, listService, _, _, _) = CreateHandler();

        await handler.HandleAsync(GroupBuyMessage("Молоко 2л"), CancellationToken.None);

        // Review message sent (with inline keyboard)
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        // Bulk add NOT called
        listService.Verify(s => s.AddItemsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Single_Item_Without_Quantity_Persists_Immediately_No_Review()
    {
        var (handler, bot, listService, history, tagCaptureService, itemRepo) = CreateHandler();

        await handler.HandleAsync(GroupBuyMessage("Молоко"), CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(10, "Молоко", null, "Alice", null), Times.Once);

        // No review keyboard message — only the plain confirmation
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);

        history.Verify(h => h.RecordAsync(
            -100L, 42L, "Alice", BotActionType.ItemAdded,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);

        tagCaptureService.Verify(s => s.StartTagCaptureAsync(
            -100L, 42L, 10, It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 99), "Молоко",
            It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Single_Item_With_Quantity_Still_Shows_Review_And_Does_Not_Persist()
    {
        var (handler, bot, listService, _, _, itemRepo) = CreateHandler();

        await handler.HandleAsync(GroupBuyMessage("Молоко 2л"), CancellationToken.None);

        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Comma_In_Args_Calls_AddItemsAsync()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = null, AddedByName = "Alice" },
            new() { Id = 2, GroupId = 10, Name = "Яйца", Quantity = null, AddedByName = "Alice" },
        };

        var (handler, bot, listService, _, _, _) = CreateHandler();
        listService.Setup(s => s.AddItemsAsync("Молоко, Яйца", 10, "Alice"))
            .ReturnsAsync(items.AsReadOnly());

        await handler.HandleAsync(GroupBuyMessage("Молоко, Яйца"), CancellationToken.None);

        listService.Verify(s => s.AddItemsAsync("Молоко, Яйца", 10, "Alice"), Times.Once);
    }

    // TDD — bug #5: degenerate bulk input like "/buy ," contains a comma (so it takes the bulk path)
    // but AddItemsAsync filters out the empty segments and returns nothing. The handler must not send a
    // "0 items added" confirmation or start a category prompt for an empty item set (which would leave a
    // dangling dialog that swallows the user's next message).
    [Fact]
    public async Task Empty_Bulk_Result_Skips_Confirmation_And_Category_Prompt()
    {
        var (handler, bot, listService, history, tagCaptureService, _) = CreateHandler();
        listService.Setup(s => s.AddItemsAsync(It.IsAny<string>(), 10, "Alice"))
            .ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        await handler.HandleAsync(GroupBuyMessage(","), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        tagCaptureService.Verify(s => s.StartTagCaptureAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Bulk_Add_Records_History_Per_Item()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = "2л", AddedByName = "Alice" },
            new() { Id = 2, GroupId = 10, Name = "Яйца", Quantity = null, AddedByName = "Alice" },
        };

        var (handler, _, listService, history, tagCaptureService, _) = CreateHandler();
        listService.Setup(s => s.AddItemsAsync(It.IsAny<string>(), 10, "Alice"))
            .ReturnsAsync(items.AsReadOnly());

        await handler.HandleAsync(GroupBuyMessage("Молоко 2л, Яйца"), CancellationToken.None);

        history.Verify(h => h.RecordAsync(
            -100L, 42L, "Alice", BotActionType.ItemAdded,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        tagCaptureService.Verify(s => s.StartTagCaptureAsync(
            -100L, 42L, 10, It.Is<IReadOnlyList<int>>(ids => ids.Count == 2 && ids[0] == 1 && ids[1] == 2), It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Bulk_Add_Sends_Confirmation_With_BulkAdded_Key()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Молоко", Quantity = null, AddedByName = "Alice" },
            new() { Id = 2, GroupId = 10, Name = "Яйца", Quantity = null, AddedByName = "Alice" },
        };

        var (handler, bot, listService, _, _, _) = CreateHandler();
        listService.Setup(s => s.AddItemsAsync(It.IsAny<string>(), 10, "Alice"))
            .ReturnsAsync(items.AsReadOnly());

        var sentTexts = new List<string?>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sentTexts.Add(smr.Text);
            })
            .ReturnsAsync(new Message());

        await handler.HandleAsync(GroupBuyMessage("Молоко, Яйца"), CancellationToken.None);

        Assert.Contains(sentTexts, t => t is not null && t.Contains("shop.bulk-added"));
    }

    [Fact]
    public async Task Bulk_Add_History_Failure_Does_Not_Suppress_Confirmation()
    {
        var items = new List<ShoppingItem>
        {
            new() { Id = 1, GroupId = 10, Name = "Хлеб", Quantity = null, AddedByName = "Alice" },
        };

        var (handler, bot, listService, history, _, _) = CreateHandler();
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
        var (handler, bot, _, _, _, _) = CreateHandler();

        await handler.HandleAsync(GroupBuyMessageNoArgs(), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
