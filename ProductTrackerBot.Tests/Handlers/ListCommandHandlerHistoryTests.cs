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
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class ListCommandHandlerHistoryTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Message GroupListMessage() =>
        DeserializeMessage("{\"message_id\":1,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"/list\"}");

    private static Mock<ITelegramBotClient> CreateBotMock(List<string>? capturedTexts = null)
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (capturedTexts != null && req is SendMessageRequest smr) capturedTexts.Add(smr.Text);
            })
            .ReturnsAsync(new Message());
        return botMock;
    }

    private static (ListCommandHandler Handler, Mock<IHistoryRepository> HistoryMock) CreateHandler(
        ITelegramBotClient bot,
        IHistoryRepository historyRepo)
    {
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        var listService = new ShoppingListService(groupRepo.Object, itemRepo.Object, Mock.Of<ILocalizer>());

        var historyMock = Mock.Get(historyRepo);
        var handler = new ListCommandHandler(bot, listService, groupRepo.Object, historyRepo, Mock.Of<ILogger<ListCommandHandler>>());
        return (handler, historyMock);
    }

    [Fact]
    public async Task Calls_RecordAsync_With_ListViewed()
    {
        var bot = CreateBotMock();
        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (handler, history) = CreateHandler(bot.Object, historyMock.Object);
        await handler.HandleAsync(GroupListMessage(), CancellationToken.None);

        history.Verify(
            h => h.RecordAsync(-100L, 42L, "Alice", BotActionType.ListViewed, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Swallows_RecordAsync_Failure()
    {
        var bot = CreateBotMock();
        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var (handler, _) = CreateHandler(bot.Object, historyMock.Object);
        await handler.HandleAsync(GroupListMessage(), CancellationToken.None);
    }
}
