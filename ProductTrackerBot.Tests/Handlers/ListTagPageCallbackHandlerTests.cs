using ProductTrackerBot.Localization;
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

public class ListTagPageCallbackHandlerTests
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

    private static ListTagPageCallbackHandler CreateHandler(
        ITelegramBotClient bot,
        IReadOnlyList<ShoppingItem> items,
        IReadOnlyList<string> tags)
    {
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.GetAllAsync(10)).ReturnsAsync(items);

        var tagRepo = new Mock<TagRepository>("Data Source=file::memory:");
        tagRepo.Setup(r => r.GetDistinctTagsAsync(10)).ReturnsAsync(tags);

        var localizer = CreateLocalizerMock();
        var listService = new ShoppingListService(groupRepo.Object, itemRepo.Object, tagRepo.Object, localizer.Object);
        var historyRepo = new Mock<IHistoryRepository>();
        return new ListTagPageCallbackHandler(
            bot,
            listService,
            groupRepo.Object,
            tagRepo.Object,
            historyRepo.Object,
            Mock.Of<ILogger<ListTagPageCallbackHandler>>());
    }

    [Fact]
    public async Task ValidTagPageTap_ShowsRequestedTagPage_PreservesItemPageAndFilter()
    {
        var bot = CreateBotMock();
        var items = new List<ShoppingItem> { new() { Id = 1, GroupId = 10, Name = "Молоко", AddedByName = "Ivan", Tags = new[] { "Tag1" } } };
        var tags = Enumerable.Range(1, 8).Select(i => $"Tag{i}").ToList();
        var handler = CreateHandler(bot.Object, items, tags);

        // Preserve active filter (index 0 = "Tag1"), item page 1, request tag page 2.
        var callback = CreateCallbackQuery("list_tagpage:-100:0:1:2");

        await handler.HandleAsync(callback, CancellationToken.None);

        // Tag page 2 shows tags 7-8 (Tag1 lives on page 1, so it isn't rendered here), while the
        // active "Tag1" filter itself is preserved — the item it matches still appears in the text,
        // and the "All Items" clear-filter row is still present.
        bot.Verify(
            b => b.SendRequest(
                It.Is<EditMessageTextRequest>(r =>
                    r.Text.Contains("Молоко")
                    && r.ReplyMarkup!.InlineKeyboard.SelectMany(row => row).Any(btn => btn.Text == "Tag7")
                    && r.ReplyMarkup!.InlineKeyboard.SelectMany(row => row).Any(btn => btn.Text == "Tag8")
                    && !r.ReplyMarkup!.InlineKeyboard.SelectMany(row => row).Any(btn => btn.Text == "Tag2")
                    && r.ReplyMarkup!.InlineKeyboard.SelectMany(row => row).Any(btn => btn.CallbackData == "list_filter:-100:-1:1:1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidData_IgnoresCallback()
    {
        var bot = CreateBotMock();
        var handler = CreateHandler(bot.Object, new List<ShoppingItem>(), Array.Empty<string>());
        var callback = CreateCallbackQuery("list_tagpage:invalid");

        await handler.HandleAsync(callback, CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ILocalizer> CreateLocalizerMock()
    {
        var mock = new Mock<ILocalizer>();
        mock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);
        return mock;
    }
}
