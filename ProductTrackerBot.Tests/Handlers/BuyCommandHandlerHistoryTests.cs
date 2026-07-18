using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class BuyCommandHandlerHistoryTests
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

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((_, _) => { })
            .ReturnsAsync(new Message());
        return botMock;
    }

    private static BuyCommandHandler CreateHandler(
        ITelegramBotClient bot,
        GroupRepository groupRepo,
        PendingAddService pendingAddService)
    {
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var tagRepo = new Mock<TagRepository>("Data Source=file::memory:");
        tagRepo.Setup(r => r.GetTopTagsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());

        var historyRepository = Mock.Of<IHistoryRepository>();
        var tagCaptureService = new TagCaptureService(bot, new PendingDialogService<TagCaptureDialogState>(), new PendingDialogService<PriceCaptureDialogState>(), tagRepo.Object, localizer.Object);
        var buyAddService = new BuyAddService(
            bot,
            new Mock<ShoppingItemRepository>("Data Source=file::memory:").Object,
            historyRepository,
            tagCaptureService,
            localizer.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<BuyAddService>>());

        return new BuyCommandHandler(
            bot,
            groupRepo,
            new PendingDialogService<BuyDialogState>(),
            pendingAddService,
            localizer.Object,
            new ShoppingListService(
                new Mock<GroupRepository>("Data Source=file::memory:").Object,
                new Mock<ShoppingItemRepository>("Data Source=file::memory:").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
                localizer.Object),
            historyRepository,
            tagCaptureService,
            buyAddService,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<BuyCommandHandler>>());
    }

    [Fact]
    public async Task Inline_Buy_Stores_In_PendingAddService_And_Sends_Review()
    {
        var bot = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        var pendingAddService = new PendingAddService();

        var handler = CreateHandler(bot.Object, groupRepo.Object, pendingAddService);
        await handler.HandleAsync(GroupBuyMessage("Молоко 2л"), CancellationToken.None);

        // No AddAsync calls — item should be pending
        itemRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<DateOnly?>()), Times.Never);

        // Review message was sent
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Inline_Buy_With_No_Args_Starts_Dialog()
    {
        var bot = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });

        var pendingAddService = new PendingAddService();
        var handler = CreateHandler(bot.Object, groupRepo.Object, pendingAddService);

        var message = DeserializeMessage("{\"message_id\":1,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"/buy\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
