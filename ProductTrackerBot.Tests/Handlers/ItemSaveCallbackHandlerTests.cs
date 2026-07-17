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
using Telegram.Bot.Types.ReplyMarkups;

namespace ProductTrackerBot.Tests.Handlers;

public class ItemSaveCallbackHandlerTests
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
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return bot;
    }

    private static Mock<CategoryCaptureService> CreateCategoryCaptureServiceMock(Mock<ITelegramBotClient> bot, ILocalizer localizer)
    {
        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        purchaseRepo.Setup(r => r.GetTopCategoriesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());
        var mock = new Mock<CategoryCaptureService>(bot.Object, new PendingDialogService<CategoryCaptureDialogState>(), purchaseRepo.Object, localizer);
        mock.Setup(s => s.StartCategoryCaptureAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static Mock<ShoppingListService> CreateListServiceMock()
    {
        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        var localizer = Mock.Of<ILocalizer>();
        var listServiceMock = new Mock<ShoppingListService>(groupRepo.Object, itemRepo.Object, localizer);
        listServiceMock
            .Setup(s => s.BuildListAsync(It.IsAny<long>(), It.IsAny<int>(), null))
            .ReturnsAsync(("list", null, new Group { Id = 10, ChatId = -100L }));
        return listServiceMock;
    }

    [Fact]
    public async Task ValidToken_CallsUpdateAsync_SendsConfirm_RecordsHistory()
    {
        var bot = CreateBotMock();
        var pendingEditService = new PendingEditService();
        var token = pendingEditService.Store(new PendingEditItem(
            ChatId: -100L,
            ItemId: 5,
            GroupId: 10,
            Name: "отривин",
            Quantity: "2 шт"));

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.UpdateAsync(5, "отривин", "2 шт")).Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var categoryCaptureServiceMock = CreateCategoryCaptureServiceMock(bot, localizer.Object);

        var handler = new ItemSaveCallbackHandler(
            bot.Object,
            pendingEditService,
            itemRepo.Object,
            CreateListServiceMock().Object,
            historyMock.Object,
            categoryCaptureServiceMock.Object,
            localizer.Object,
            Mock.Of<ILogger<ItemSaveCallbackHandler>>());

        var cbQuery = DeserializeCallbackQuery(
            $"{{\"id\":\"cb1\",\"from\":{{\"id\":42,\"first_name\":\"Alice\"}}," +
            $"\"message\":{{\"message_id\":5,\"chat\":{{\"id\":-100}},\"text\":\"test\"}}," +
            $"\"data\":\"item:save:{token}\"}}");

        await handler.HandleAsync(cbQuery, CancellationToken.None);

        itemRepo.Verify(r => r.UpdateAsync(5, "отривин", "2 шт"), Times.Once);
        categoryCaptureServiceMock.Verify(s => s.StartCategoryCaptureAsync(
            -100L, 42L, 10, It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 5), "отривин", It.IsAny<CancellationToken>()),
            Times.Once);
        historyMock.Verify(h => h.RecordAsync(
            -100L, 42L, "Alice", BotActionType.ItemEdited,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExpiredToken_AnswersWithError_DoesNotCallUpdateAsync()
    {
        var bot = CreateBotMock();
        var pendingEditService = new PendingEditService();

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        var historyMock = new Mock<IHistoryRepository>();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new ItemSaveCallbackHandler(
            bot.Object,
            pendingEditService,
            itemRepo.Object,
            CreateListServiceMock().Object,
            historyMock.Object,
            CreateCategoryCaptureServiceMock(bot, localizer.Object).Object,
            localizer.Object,
            Mock.Of<ILogger<ItemSaveCallbackHandler>>());

        var cbQuery = DeserializeCallbackQuery(
            "{\"id\":\"cb2\",\"from\":{\"id\":42,\"first_name\":\"Alice\"}," +
            "\"message\":{\"message_id\":5,\"chat\":{\"id\":-100},\"text\":\"test\"}," +
            "\"data\":\"item:save:deadbeef\"}");

        await handler.HandleAsync(cbQuery, CancellationToken.None);

        itemRepo.Verify(r => r.UpdateAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        bot.Verify(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
