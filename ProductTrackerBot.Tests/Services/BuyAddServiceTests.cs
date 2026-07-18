using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Services;

public class BuyAddServiceTests
{
    private static Mock<TagCaptureService> CreateTagCaptureServiceMock(Mock<ITelegramBotClient> bot, Mock<ILocalizer> localizer)
    {
        var tagRepo = new Mock<TagRepository>("Data Source=file::memory:");
        tagRepo.Setup(r => r.GetTopTagsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());
        var mock = new Mock<TagCaptureService>(bot.Object, new PendingDialogService<TagCaptureDialogState>(), tagRepo.Object, localizer.Object);
        mock.Setup(s => s.StartTagCaptureAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task AddAndConfirmAsync_NoQuantity_PersistsItem_SendsPlainConfirmation_RecordsHistory_StartsTagCapture()
    {
        var bot = new Mock<ITelegramBotClient>();
        var sentTexts = new List<string?>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sentTexts.Add(smr.Text);
            })
            .ReturnsAsync(new Message());

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(10, "Молоко", null, "Alice", null))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Молоко", Quantity = null, AddedByName = "Alice" });

        var history = new Mock<IHistoryRepository>();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var tagCaptureServiceMock = CreateTagCaptureServiceMock(bot, localizer);

        var service = new BuyAddService(
            bot.Object, itemRepo.Object, history.Object, tagCaptureServiceMock.Object,
            localizer.Object, Mock.Of<ILogger<BuyAddService>>());

        var item = await service.AddAndConfirmAsync(
            chatId: -100L, userId: 42L, groupId: 10, name: "Молоко", quantity: null, addedByName: "Alice",
            cancellationToken: CancellationToken.None);

        Assert.Equal("Молоко", item.Name);
        itemRepo.Verify(r => r.AddAsync(10, "Молоко", null, "Alice", null), Times.Once);
        Assert.Contains(sentTexts, t => t == "buy.item-added");
        history.Verify(h => h.RecordAsync(
            -100L, 42L, "Alice", BotActionType.ItemAdded,
            It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
        tagCaptureServiceMock.Verify(s => s.StartTagCaptureAsync(
            -100L, 42L, 10, It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 1), "Молоко",
            null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddAndConfirmAsync_WithQuantity_PersistsItem_SendsQuantityConfirmation()
    {
        var bot = new Mock<ITelegramBotClient>();
        var sentTexts = new List<string?>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sentTexts.Add(smr.Text);
            })
            .ReturnsAsync(new Message());

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(10, "Молоко", "2л", "Alice", null))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Молоко", Quantity = "2л", AddedByName = "Alice" });

        var history = new Mock<IHistoryRepository>();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var tagCaptureServiceMock = CreateTagCaptureServiceMock(bot, localizer);

        var service = new BuyAddService(
            bot.Object, itemRepo.Object, history.Object, tagCaptureServiceMock.Object,
            localizer.Object, Mock.Of<ILogger<BuyAddService>>());

        await service.AddAndConfirmAsync(
            chatId: -100L, userId: 42L, groupId: 10, name: "Молоко", quantity: "2л", addedByName: "Alice",
            cancellationToken: CancellationToken.None);

        Assert.Contains(sentTexts, t => t == "buy.item-added-quantity");
    }

    [Fact]
    public async Task AddAndConfirmAsync_HistoryFailure_DoesNotSuppressConfirmationOrTagCapture()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file::memory:");
        itemRepo.Setup(r => r.AddAsync(10, "Хлеб", null, "Alice", null))
            .ReturnsAsync(new ShoppingItem { Id = 5, GroupId = 10, Name = "Хлеб", Quantity = null, AddedByName = "Alice" });

        var history = new Mock<IHistoryRepository>();
        history.Setup(h => h.RecordAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("db failure"));

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var tagCaptureServiceMock = CreateTagCaptureServiceMock(bot, localizer);

        var service = new BuyAddService(
            bot.Object, itemRepo.Object, history.Object, tagCaptureServiceMock.Object,
            localizer.Object, Mock.Of<ILogger<BuyAddService>>());

        await service.AddAndConfirmAsync(
            chatId: -100L, userId: 42L, groupId: 10, name: "Хлеб", quantity: null, addedByName: "Alice",
            cancellationToken: CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        tagCaptureServiceMock.Verify(s => s.StartTagCaptureAsync(
            -100L, 42L, 10, It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 5), "Хлеб",
            null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
