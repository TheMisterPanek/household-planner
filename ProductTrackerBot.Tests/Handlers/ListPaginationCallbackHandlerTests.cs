using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class ListPaginationCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery DeserializeCallbackQuery(string json) =>
        JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;

    private static CallbackQuery CreateCallbackQuery(string callbackData) =>
        DeserializeCallbackQuery($"{{\"id\":\"cb1\",\"from\":{{\"id\":42,\"first_name\":\"Alice\"}},\"chat_instance\":\"123\",\"message\":{{\"message_id\":1,\"chat\":{{\"id\":-100}},\"text\":\"Shopping list\"}},\"data\":\"{callbackData}\"}}");

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock
            .Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        botMock
            .Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return botMock;
    }

    private static (ListPrevCallbackHandler Handler, Mock<IHistoryRepository> HistoryMock) CreatePrevHandler(
        ITelegramBotClient bot)
    {
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        var listService = new ShoppingListService(groupRepo.Object, itemRepo.Object);
        var handler = new ListPrevCallbackHandler(bot, listService, groupRepo.Object, Mock.Of<ILogger<ListPrevCallbackHandler>>());
        return (handler, null);
    }

    private static ListNextCallbackHandler CreateNextHandler(ITelegramBotClient bot)
    {
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(new List<ShoppingItem>().AsReadOnly());

        var listService = new ShoppingListService(groupRepo.Object, itemRepo.Object);
        return new ListNextCallbackHandler(bot, listService, groupRepo.Object, Mock.Of<ILogger<ListNextCallbackHandler>>());
    }

    [Fact]
    public async Task ListPrevCallbackHandler_ValidCallback_EditsMessage()
    {
        var bot = CreateBotMock();
        var (handler, _) = CreatePrevHandler(bot.Object);
        var callback = CreateCallbackQuery("list_prev:-100:1");

        await handler.HandleAsync(callback, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListNextCallbackHandler_ValidCallback_EditsMessage()
    {
        var bot = CreateBotMock();
        var handler = CreateNextHandler(bot.Object);
        var callback = CreateCallbackQuery("list_next:-100:2");

        await handler.HandleAsync(callback, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListPrevCallbackHandler_InvalidData_IgnoresCallback()
    {
        var bot = CreateBotMock();
        var (handler, _) = CreatePrevHandler(bot.Object);
        var callback = CreateCallbackQuery("list_prev:invalid");

        await handler.HandleAsync(callback, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
