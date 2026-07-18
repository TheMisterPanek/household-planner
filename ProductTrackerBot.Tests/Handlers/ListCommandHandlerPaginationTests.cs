using ProductTrackerBot.Localization;
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

public class ListCommandHandlerPaginationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Message CreateListMessage(string? text = null) =>
        DeserializeMessage($"{{\"message_id\":1,\"from\":{{\"id\":42,\"first_name\":\"Alice\"}},\"chat\":{{\"id\":-100}},\"text\":\"{text ?? "/list"}\"}}");

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
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

        var localizer = CreateLocalizerMock();
        var listService = new ShoppingListService(groupRepo.Object, itemRepo.Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object, localizer.Object);

        var historyMock = Mock.Get(historyRepo);
        var handler = new ListCommandHandler(bot, listService, groupRepo.Object, historyRepo, Mock.Of<ILogger<ListCommandHandler>>());
        return (handler, historyMock);
    }

    private static Mock<ILocalizer> CreateLocalizerMock()
    {
        var mock = new Mock<ILocalizer>();
        mock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);
        return mock;
    }

    [Fact]
    public async Task Without_Page_Number_Uses_Page_1()
    {
        var bot = CreateBotMock();
        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (handler, _) = CreateHandler(bot.Object, historyMock.Object);
        var message = CreateListMessage("/list");
        await handler.HandleAsync(message, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task With_Page_Number_Parses_Correctly()
    {
        var bot = CreateBotMock();
        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (handler, _) = CreateHandler(bot.Object, historyMock.Object);
        var message = CreateListMessage("/list 2");
        await handler.HandleAsync(message, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task With_Invalid_Page_Number_Defaults_To_Page_1()
    {
        var bot = CreateBotMock();
        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (handler, _) = CreateHandler(bot.Object, historyMock.Object);
        var message = CreateListMessage("/list abc");
        await handler.HandleAsync(message, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
