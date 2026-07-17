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
using Telegram.Bot.Types.ReplyMarkups;

namespace ProductTrackerBot.Tests.Handlers;

public class CategoryCaptureDialogTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        botMock.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        botMock.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return botMock;
    }

    private static Mock<ILocalizer> CreateLocalizerMock()
    {
        var localizerMock = new Mock<ILocalizer>();
        localizerMock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);
        localizerMock.Setup(l => l.Get(It.IsAny<long>(), "category.prompt"))
            .Returns("В какую группу добавить {item}?");
        localizerMock.Setup(l => l.Get(It.IsAny<long>(), "category.set-confirmation"))
            .Returns("✓ Категория «{category}» установлена");
        return localizerMock;
    }

    private static CallbackQuery CreateCallback(string data, long chatId = -100, int userId = 42, string firstName = "Alice")
    {
        return new CallbackQuery
        {
            Id = "cb1",
            Data = data,
            From = new User { Id = userId, FirstName = firstName },
            Message = DeserializeMessage(
                "{\"message_id\":1,\"chat\":{\"id\":" + chatId + ",\"type\":\"supergroup\"}}"),
        };
    }

    [Fact]
    public async Task CategoryCaptureService_With_Suggestions_Sends_Prompt_With_Buttons_And_Skip()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopCategoriesAsync(10, 5))
            .ReturnsAsync(new List<string> { "Химия", "Авто" });

        var service = new CategoryCaptureService(bot.Object, dialogService, purchaseRepo.Object, CreateLocalizerMock().Object);

        await service.StartCategoryCaptureAsync(-100L, 42L, 10, new[] { 1 }, "Молоко", CancellationToken.None);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(new List<int> { 1 }, state!.ItemIds);
        Assert.Equal("Молоко", state.ItemLabel);
        Assert.Equal(10, state.GroupId);
        Assert.NotNull(state.TopCategories);
        Assert.Equal(2, state.TopCategories!.Count);

        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("Молоко") &&
                r.ReplyMarkup != null &&
                ((InlineKeyboardMarkup?)r.ReplyMarkup)!.InlineKeyboard.Count() == 3), // 2 suggestions + skip
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategoryCaptureService_With_No_Suggestions_Sends_Prompt_With_Only_Skip()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopCategoriesAsync(10, 5))
            .ReturnsAsync(new List<string>());

        var service = new CategoryCaptureService(bot.Object, dialogService, purchaseRepo.Object, CreateLocalizerMock().Object);

        await service.StartCategoryCaptureAsync(-100L, 42L, 10, new[] { 1 }, "Молоко", CancellationToken.None);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Null(state!.TopCategories);

        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.ReplyMarkup != null &&
                ((InlineKeyboardMarkup?)r.ReplyMarkup)!.InlineKeyboard.Count() == 1), // only skip
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategoryCaptureService_Bulk_Stores_All_ItemIds()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopCategoriesAsync(10, 5))
            .ReturnsAsync(new List<string>());

        var service = new CategoryCaptureService(bot.Object, dialogService, purchaseRepo.Object, CreateLocalizerMock().Object);

        await service.StartCategoryCaptureAsync(-100L, 42L, 10, new[] { 1, 2, 3 }, "3 товара", CancellationToken.None);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(new List<int> { 1, 2, 3 }, state!.ItemIds);
        Assert.Equal("3 товара", state.ItemLabel);
    }

    [Fact]
    public async Task CategorySuggestCallbackHandler_Tapping_Suggestion_Applies_Category_And_Clears_State()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new CategoryCaptureDialogState
        {
            ItemIds = new List<int> { 1 },
            ItemLabel = "Молоко",
            GroupId = 10,
            TopCategories = new List<string> { "Молочка", "Химия" },
        });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.UpdateCategoryAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var handler = new CategorySuggestCallbackHandler(
            bot.Object, dialogService, itemRepo.Object, CreateLocalizerMock().Object,
            Mock.Of<ILogger<CategorySuggestCallbackHandler>>());

        var callback = CreateCallback("category:suggest:0");

        await handler.HandleAsync(callback, CancellationToken.None);

        itemRepo.Verify(r => r.UpdateCategoryAsync(
            It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 1), "Молочка"), Times.Once);
        Assert.Null(dialogService.GetState(-100L, 42L));
    }

    [Fact]
    public async Task CategorySuggestCallbackHandler_Invalid_Index_Does_Not_Apply_Category()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new CategoryCaptureDialogState
        {
            ItemIds = new List<int> { 1 },
            ItemLabel = "Молоко",
            GroupId = 10,
            TopCategories = new List<string> { "Молочка" },
        });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");

        var handler = new CategorySuggestCallbackHandler(
            bot.Object, dialogService, itemRepo.Object, CreateLocalizerMock().Object,
            Mock.Of<ILogger<CategorySuggestCallbackHandler>>());

        var callback = CreateCallback("category:suggest:5");

        await handler.HandleAsync(callback, CancellationToken.None);

        itemRepo.Verify(r => r.UpdateCategoryAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<string?>()), Times.Never);
        Assert.NotNull(dialogService.GetState(-100L, 42L));
    }

    [Fact]
    public async Task CategorySkipCallbackHandler_Clears_State_Without_Setting_Category()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new CategoryCaptureDialogState
        {
            ItemIds = new List<int> { 1 },
            ItemLabel = "Молоко",
            GroupId = 10,
        });

        var handler = new CategorySkipCallbackHandler(bot.Object, dialogService, CreateLocalizerMock().Object);

        var callback = CreateCallback("category:skip");

        await handler.HandleAsync(callback, CancellationToken.None);

        Assert.Null(dialogService.GetState(-100L, 42L));
        bot.Verify(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategoryCaptureStepHandler_CanHandle_Returns_True_When_Pending_State()
    {
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new CategoryCaptureDialogState { ItemIds = new List<int> { 1 }, GroupId = 10 });

        var handler = new CategoryCaptureStepHandler(
            CreateBotMock().Object, dialogService,
            new Mock<ShoppingItemRepository>("Data Source=file:test").Object,
            CreateLocalizerMock().Object);

        Assert.True(handler.CanHandle(-100L, 42L));
        Assert.False(handler.CanHandle(-100L, 99L));
    }

    [Fact]
    public async Task CategoryCaptureStepHandler_FreeText_Creates_New_Category_And_Clears_State()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new CategoryCaptureDialogState
        {
            ItemIds = new List<int> { 1 },
            ItemLabel = "Порошок",
            GroupId = 10,
        });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.UpdateCategoryAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var handler = new CategoryCaptureStepHandler(bot.Object, dialogService, itemRepo.Object, CreateLocalizerMock().Object);

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"Для стирки\"}");

        await handler.HandleAsync(message, CancellationToken.None);

        itemRepo.Verify(r => r.UpdateCategoryAsync(
            It.Is<IReadOnlyList<int>>(ids => ids.Count == 1 && ids[0] == 1), "Для стирки"), Times.Once);
        Assert.Null(dialogService.GetState(-100L, 42L));

        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("Для стирки")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategoryCaptureStepHandler_Bulk_Applies_Category_To_All_ItemIds()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<CategoryCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new CategoryCaptureDialogState
        {
            ItemIds = new List<int> { 1, 2, 3 },
            ItemLabel = "3 товара",
            GroupId = 10,
        });

        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.UpdateCategoryAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var handler = new CategoryCaptureStepHandler(bot.Object, dialogService, itemRepo.Object, CreateLocalizerMock().Object);

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"Молочка\"}");

        await handler.HandleAsync(message, CancellationToken.None);

        itemRepo.Verify(r => r.UpdateCategoryAsync(
            It.Is<IReadOnlyList<int>>(ids => ids.Count == 3 && ids.SequenceEqual(new[] { 1, 2, 3 })), "Молочка"), Times.Once);
    }
}
