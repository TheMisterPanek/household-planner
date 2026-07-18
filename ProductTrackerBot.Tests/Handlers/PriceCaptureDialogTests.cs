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
using Telegram.Bot.Types.ReplyMarkups;

namespace ProductTrackerBot.Tests.Handlers;

public class PriceCaptureDialogTests
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
        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((_, _) => { })
            .ReturnsAsync(new Message());
        return botMock;
    }

    private static Mock<PurchaseHistoryRepository> CreatePurchaseRepoMock()
    {
        var repo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        repo.Setup(r => r.AddAsync(It.IsAny<PurchaseRecord>()))
            .ReturnsAsync((PurchaseRecord r) => { r.Id = 1; return r; });
        repo.Setup(r => r.GetTopShopsAsync(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());
        return repo;
    }

    private static Mock<GroupRepository> CreateGroupRepoMock()
    {
        var repo = new Mock<GroupRepository>("Data Source=file:test");
        repo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync((long chatId) => new Group { Id = 10, ChatId = chatId });
        return repo;
    }

    private static Mock<PriceLogRepository> CreatePriceLogRepoMock()
    {
        var repo = new Mock<PriceLogRepository>("Data Source=file:test");
        repo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((int groupId, string itemName, decimal price, string? storeName, DateTime loggedAt) =>
                new PriceLogEntry(1, groupId, itemName, price, storeName, loggedAt));
        return repo;
    }

    private static Mock<ILocalizer> CreateLocalizerMock()
    {
        var localizerMock = new Mock<ILocalizer>();
        localizerMock.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns((long _, string key) => key);
        return localizerMock;
    }

    private static ExpiryDaySuggestionService CreateSuggestionService()
    {
        var repo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        repo.Setup(r => r.GetExpiryDaySuggestionsAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<int>().AsReadOnly());
        repo.Setup(r => r.GetAverageExpiryDaysAsync(It.IsAny<int>()))
            .ReturnsAsync(0);
        return new ExpiryDaySuggestionService(repo.Object);
    }

    private static CallbackQuery CreateCallback(string data, long chatId = -100, int userId = 42, string firstName = "Alice")
    {
        return new CallbackQuery
        {
            Id = "cb1",
            Data = data,
            From = new User { Id = userId, FirstName = firstName },
            Message = DeserializeMessage(
                "{\"message_id\":1,\"chat\":{\"id\":" + chatId + ",\"type\":\"supergroup\"},\"reply_markup\":{\"inline_keyboard\":[[{\"text\":\"✓ Milk 2л\",\"callback_data\":\"shop:done:1\"}]]}}"),
        };
    }

    [Fact]
    public async Task ShopDoneCallbackHandler_Sets_PriceCaptureDialog_And_Sends_Store_Prompt()
    {
        // Arrange
        var bot = CreateBotMock();
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Milk", Quantity = "2л", AddedByName = "Alice" });
        itemRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        var listService = new Mock<ShoppingListService>(
            CreateGroupRepoMock().Object,
            new Mock<ShoppingItemRepository>("Data Source=file:test").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());
        listService.Setup(s => s.BuildListAsync(-100L, It.IsAny<int>(), null))
            .ReturnsAsync(("list text", (InlineKeyboardMarkup?)null, new Group { Id = 10, ChatId = -100L }));

        var groupRepo = CreateGroupRepoMock();
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopShopsAsync(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());

        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();

        var handler = new ShopDoneCallbackHandler(
            bot.Object,
            itemRepo.Object,
            listService.Object,
            groupRepo.Object,
            historyMock.Object,
            priceDialogService,
            new PendingDialogService<TagCaptureDialogState>(),
            purchaseRepo.Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<ShopDoneCallbackHandler>>());

        var callback = CreateCallback("shop:done:1");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - dialog state is set with step=1
        var state = priceDialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(1, state!.Step);
        Assert.Equal("Milk", state.ItemName);
        Assert.Equal("2л", state.Quantity);
        Assert.Equal("Alice", state.BoughtByName);

        // Assert - store prompt was sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("shop.where-bought") &&
                r.ReplyMarkup != null),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - item was deleted
        itemRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task ShopDoneCallbackHandler_Clears_Stale_TagCaptureDialog()
    {
        // Regression test: a leftover tag-capture dialog (e.g. the user never replied to a
        // "what tags?" prompt for a different item) must not survive into a newly started
        // price-capture dialog — otherwise a later reply meant for the price dialog could be
        // captured by the stale tag prompt instead.
        var bot = CreateBotMock();
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Milk", Quantity = "2л", AddedByName = "Alice" });
        itemRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        var listService = new Mock<ShoppingListService>(
            CreateGroupRepoMock().Object,
            new Mock<ShoppingItemRepository>("Data Source=file:test").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());
        listService.Setup(s => s.BuildListAsync(-100L, It.IsAny<int>(), null))
            .ReturnsAsync(("list text", (InlineKeyboardMarkup?)null, new Group { Id = 10, ChatId = -100L }));

        var groupRepo = CreateGroupRepoMock();
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopShopsAsync(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());

        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();
        var tagDialogService = new PendingDialogService<TagCaptureDialogState>();
        tagDialogService.SetState(-100L, 42L, new TagCaptureDialogState { ItemLabel = "Cola" });

        var handler = new ShopDoneCallbackHandler(
            bot.Object,
            itemRepo.Object,
            listService.Object,
            groupRepo.Object,
            historyMock.Object,
            priceDialogService,
            tagDialogService,
            purchaseRepo.Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<ShopDoneCallbackHandler>>());

        var callback = CreateCallback("shop:done:1");

        await handler.HandleAsync(callback, CancellationToken.None);

        Assert.Null(tagDialogService.GetState(-100L, 42L));
        Assert.NotNull(priceDialogService.GetState(-100L, 42L));
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Step1_Saves_Store_And_Asks_For_Price()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 1,
            ItemName = "Milk",
            Quantity = "2л",
            BoughtByName = "Alice",
        });

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            CreatePurchaseRepoMock().Object,
            CreatePriceLogRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"Magnit\"}");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - store saved and step advanced
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Equal("Magnit", state.StoreName);

        // Assert - price prompt sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("shop.price-for") &&
                r.ReplyMarkup != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Step2_With_Valid_Decimal_Saves_Record_And_Clears_Dialog()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 2,
            ItemName = "Milk",
            Quantity = "2л",
            StoreName = "Magnit",
            BoughtByName = "Alice",
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var priceLogRepo = CreatePriceLogRepoMock();
        var groupRepo = CreateGroupRepoMock();

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            priceLogRepo.Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"89.90\"}");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - record saved with correct data
        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ItemName == "Milk" &&
            p.Price == 89.90m &&
            p.StoreName == "Magnit" &&
            p.BoughtByName == "Alice" &&
            p.GroupId == 10)), Times.Once);

        // Assert - dialog cleared
        Assert.Null(dialogService.GetState(-100L, 42L));

        // Assert - confirmation sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("shop.recorded")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Step2_With_Invalid_Input_Shows_Retry_And_Stays_On_Step2()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 2,
            ItemName = "Milk",
            BoughtByName = "Alice",
        });

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            CreatePurchaseRepoMock().Object,
            CreatePriceLogRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"not a price\"}");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - dialog still active on step 2
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);

        // Assert - retry message sent, no record saved
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("shop.invalid-price")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceSkipCallbackHandler_Skip_Store_Advances_To_Step2()
    {
        // Arrange
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 1,
            ItemName = "Milk",
            BoughtByName = "Alice",
        });

        var handler = new PriceSkipCallbackHandler(
            bot.Object,
            dialogService,
            CreatePurchaseRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceSkipCallbackHandler>>());

        var callback = CreateCallback("price:skip_store");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - advanced to step 2
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Null(state.StoreName);

        // Assert - price prompt sent with skip button
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("shop.price-for")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceSkipCallbackHandler_Skip_Price_Advances_To_Step3_For_Expiry()
    {
        // Arrange
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 2,
            ItemName = "Milk",
            Quantity = "2л",
            StoreName = "Magnit",
            BoughtByName = "Alice",
        });

        var handler = new PriceSkipCallbackHandler(
            bot.Object,
            dialogService,
            CreatePurchaseRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceSkipCallbackHandler>>());

        var callback = CreateCallback("price:skip_price");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - advanced to step 3
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(3, state!.Step);
        Assert.Null(state.Price);

        // Assert - expiry prompt sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("shop.expiry-prompt") &&
                r.ReplyMarkup != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceSkipCallbackHandler_Skip_Expiry_Saves_Record_With_Null_ExpDate_And_Clears_Dialog()
    {
        // Arrange
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 3,
            ItemName = "Yogurt",
            Quantity = "400g",
            StoreName = "Carrefour",
            Price = 45.50m,
            BoughtByName = "Alice",
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var groupRepo = CreateGroupRepoMock();

        var handler = new PriceSkipCallbackHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceSkipCallbackHandler>>());

        var callback = CreateCallback("price:skip_expiry");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - record saved with null expiry
        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ItemName == "Yogurt" &&
            p.Price == 45.50m &&
            p.ExpDate == null &&
            p.StoreName == "Carrefour" &&
            p.BoughtByName == "Alice" &&
            p.GroupId == 10)), Times.Once);

        // Assert - dialog cleared
        Assert.Null(dialogService.GetState(-100L, 42L));

        // Assert - confirmation sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("shop.recorded")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShopDoneCallbackHandler_Fetches_Top_Shops_And_Stores_In_State()
    {
        // Arrange
        var bot = CreateBotMock();
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Milk", Quantity = "2л", AddedByName = "Alice" });
        itemRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        var listService = new Mock<ShoppingListService>(
            CreateGroupRepoMock().Object,
            new Mock<ShoppingItemRepository>("Data Source=file:test").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());
        listService.Setup(s => s.BuildListAsync(-100L, It.IsAny<int>(), null))
            .ReturnsAsync(("list text", (InlineKeyboardMarkup?)null, new Group { Id = 10, ChatId = -100L }));

        var groupRepo = CreateGroupRepoMock();
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopShopsAsync(10, 42, 5))
            .ReturnsAsync(new List<string> { "Carrefour", "Stokrotka" });

        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();

        var handler = new ShopDoneCallbackHandler(
            bot.Object,
            itemRepo.Object,
            listService.Object,
            groupRepo.Object,
            historyMock.Object,
            priceDialogService,
            new PendingDialogService<TagCaptureDialogState>(),
            purchaseRepo.Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<ShopDoneCallbackHandler>>());

        var callback = CreateCallback("shop:done:1");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - top shops fetched and stored in state
        var state = priceDialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.NotNull(state!.TopShops);
        Assert.Equal(2, state.TopShops.Count);
        Assert.Equal("Carrefour", state.TopShops[0]);
        Assert.Equal("Stokrotka", state.TopShops[1]);
    }

    [Fact]
    public async Task ShopDoneCallbackHandler_Creates_Shop_Buttons_From_TopShops()
    {
        // Arrange
        var bot = CreateBotMock();
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Milk", Quantity = "2л", AddedByName = "Alice" });
        itemRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        var listService = new Mock<ShoppingListService>(
            CreateGroupRepoMock().Object,
            new Mock<ShoppingItemRepository>("Data Source=file:test").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());
        listService.Setup(s => s.BuildListAsync(-100L, It.IsAny<int>(), null))
            .ReturnsAsync(("list text", (InlineKeyboardMarkup?)null, new Group { Id = 10, ChatId = -100L }));

        var groupRepo = CreateGroupRepoMock();
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopShopsAsync(10, 42, 5))
            .ReturnsAsync(new List<string> { "Carrefour", "Stokrotka" });

        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();

        var handler = new ShopDoneCallbackHandler(
            bot.Object,
            itemRepo.Object,
            listService.Object,
            groupRepo.Object,
            historyMock.Object,
            priceDialogService,
            new PendingDialogService<TagCaptureDialogState>(),
            purchaseRepo.Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<ShopDoneCallbackHandler>>());

        var callback = CreateCallback("shop:done:1");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - message with shop buttons was sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("shop.where-bought") &&
                r.ReplyMarkup != null &&
                ((InlineKeyboardMarkup?)r.ReplyMarkup)!.InlineKeyboard.Count() == 3), // 2 shops + skip button
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShopDoneCallbackHandler_With_No_Top_Shops_Shows_Only_Skip_Button()
    {
        // Arrange
        var bot = CreateBotMock();
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Milk", Quantity = "2л", AddedByName = "Alice" });
        itemRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        var listService = new Mock<ShoppingListService>(
            CreateGroupRepoMock().Object,
            new Mock<ShoppingItemRepository>("Data Source=file:test").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());
        listService.Setup(s => s.BuildListAsync(-100L, It.IsAny<int>(), null))
            .ReturnsAsync(("list text", (InlineKeyboardMarkup?)null, new Group { Id = 10, ChatId = -100L }));

        var groupRepo = CreateGroupRepoMock();
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopShopsAsync(10, 42, 5))
            .ReturnsAsync(new List<string>());

        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();

        var handler = new ShopDoneCallbackHandler(
            bot.Object,
            itemRepo.Object,
            listService.Object,
            groupRepo.Object,
            historyMock.Object,
            priceDialogService,
            new PendingDialogService<TagCaptureDialogState>(),
            purchaseRepo.Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<ShopDoneCallbackHandler>>());

        var callback = CreateCallback("shop:done:1");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - message with only skip button was sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("shop.where-bought") &&
                r.ReplyMarkup != null &&
                ((InlineKeyboardMarkup?)r.ReplyMarkup)!.InlineKeyboard.Count() == 1), // only skip button
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceShopSuggestionCallbackHandler_Tapping_Shop_Button_Sets_StoreName_And_Advances_To_Step2()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 1,
            ItemName = "Milk",
            Quantity = "2л",
            BoughtByName = "Alice",
            TopShops = new List<string> { "Carrefour", "Stokrotka" },
        });

        var handler = new PriceShopSuggestionCallbackHandler(
            bot.Object,
            dialogService,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceShopSuggestionCallbackHandler>>());

        var callback = CreateCallback("price:shop:0");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - store name set and advanced to step 2
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Equal("Carrefour", state.StoreName);

        // Assert - price prompt sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("shop.price-for") &&
                r.ReplyMarkup != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Fallback_To_Custom_Shop_Name_When_User_Types_Text()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 1,
            ItemName = "Milk",
            Quantity = "2л",
            BoughtByName = "Alice",
            TopShops = new List<string> { "Carrefour", "Stokrotka" },
        });

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            CreatePurchaseRepoMock().Object,
            CreatePriceLogRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"MyCustomShop\"}");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - custom shop name saved, suggestions ignored
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Equal("MyCustomShop", state.StoreName);

        // Assert - price prompt sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("shop.price-for")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShopDoneCallbackHandler_Handles_Long_Shop_Names_With_Truncation()
    {
        // Arrange
        var bot = CreateBotMock();
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Milk", Quantity = "2л", AddedByName = "Alice" });
        itemRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        var listService = new Mock<ShoppingListService>(
            CreateGroupRepoMock().Object,
            new Mock<ShoppingItemRepository>("Data Source=file:test").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());
        listService.Setup(s => s.BuildListAsync(-100L, It.IsAny<int>(), null))
            .ReturnsAsync(("list text", (InlineKeyboardMarkup?)null, new Group { Id = 10, ChatId = -100L }));

        var groupRepo = CreateGroupRepoMock();
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var longShopName = new string('A', 40);
        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopShopsAsync(10, 42, 5))
            .ReturnsAsync(new List<string> { longShopName, "Short" });

        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();

        var handler = new ShopDoneCallbackHandler(
            bot.Object,
            itemRepo.Object,
            listService.Object,
            groupRepo.Object,
            historyMock.Object,
            priceDialogService,
            new PendingDialogService<TagCaptureDialogState>(),
            purchaseRepo.Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<ShopDoneCallbackHandler>>());

        var callback = CreateCallback("shop:done:1");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - long shop name truncated in state
        var state = priceDialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.NotNull(state!.TopShops);
        Assert.Equal(2, state.TopShops.Count);
        Assert.True(state.TopShops[0].Length <= 31); // 30 chars + ellipsis
        Assert.EndsWith("…", state.TopShops[0]);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_WithNonNullPrice_CallsPriceLogRepository()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 2,
            ItemName = "Milk",
            Quantity = "2л",
            StoreName = "Magnit",
            BoughtByName = "Alice",
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var groupRepo = CreateGroupRepoMock();
        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file:test");
        priceLogRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((int groupId, string itemName, decimal price, string? storeName, DateTime loggedAt) =>
                new PriceLogEntry(1, groupId, itemName, price, storeName, loggedAt));

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            priceLogRepo.Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"89.90\"}");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - PriceLogRepository.AddAsync called with correct parameters
        priceLogRepo.Verify(r => r.AddAsync(10, "Milk", 89.90m, "Magnit", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_WithNullPrice_DoesNotCallPriceLogRepository()
    {
        // Arrange
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 2,
            ItemName = "Milk",
            Quantity = "2л",
            StoreName = "Magnit",
            BoughtByName = "Alice",
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var groupRepo = CreateGroupRepoMock();
        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file:test");

        var skipHandler = new PriceSkipCallbackHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceSkipCallbackHandler>>());

        var callback = CreateCallback("price:skip_price");

        // Act
        await skipHandler.HandleAsync(callback, CancellationToken.None);

        // Assert - PriceLogRepository.AddAsync NOT called
        priceLogRepo.Verify(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_PriceLogRepositoryFailure_DoesNotSuppressConfirmation()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 2,
            ItemName = "Milk",
            StoreName = "Magnit",
            BoughtByName = "Alice",
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var groupRepo = CreateGroupRepoMock();
        var priceLogRepo = new Mock<PriceLogRepository>("Data Source=file:test");
        priceLogRepo.Setup(r => r.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Database error"));

        var logger = new Mock<ILogger<PriceCaptureStepHandler>>();

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            priceLogRepo.Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            logger.Object);

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"89.90\"}");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - confirmation message still sent despite price log failure
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("shop.recorded")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - dialog cleared
        Assert.Null(dialogService.GetState(-100L, 42L));

        // Assert - warning logged
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Step2_With_Valid_Price_Advances_To_Step3_For_Expiry()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 2,
            ItemName = "Milk",
            Quantity = "2л",
            StoreName = "Magnit",
            BoughtByName = "Alice",
        });

        var localizerMock = new Mock<ILocalizer>();
        localizerMock.Setup(l => l.Get(-100L, "shop.expiry-prompt"))
            .Returns("📅 Expiry date for {item}? Enter days (e.g. 30) or a variant (e.g. 1 week, 2 months) or click Skip.");

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            CreatePurchaseRepoMock().Object,
            CreatePriceLogRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            localizerMock.Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"89.90\"}");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - state advanced to step 3
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(3, state!.Step);
        Assert.Equal(89.90m, state.Price);

        // Assert - expiry prompt sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("📅 Expiry date for Milk") &&
                r.ReplyMarkup != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Step3_With_Valid_Days_Saves_Record_With_ExpDate()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 3,
            ItemName = "Yogurt",
            Quantity = "400g",
            StoreName = "Carrefour",
            Price = 45.50m,
            BoughtByName = "Alice",
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var groupRepo = CreateGroupRepoMock();

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            CreatePriceLogRepoMock().Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"30\"}");

        // Act
        var today = DateOnly.FromDateTime(DateTime.Now);
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - record saved with ExpDate
        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ItemName == "Yogurt" &&
            p.Price == 45.50m &&
            p.ExpDate == today.AddDays(30) &&
            p.StoreName == "Carrefour" &&
            p.BoughtByName == "Alice")), Times.Once);

        // Assert - dialog cleared
        Assert.Null(dialogService.GetState(-100L, 42L));

        // Assert - confirmation sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("shop.recorded")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Step3_With_Days_Format_Parses_Correctly()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 3,
            ItemName = "Milk",
            StoreName = "Magnit",
            Price = 50.00m,
            BoughtByName = "Alice",
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var groupRepo = CreateGroupRepoMock();

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            CreatePriceLogRepoMock().Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"7 days\"}");

        // Act
        var today = DateOnly.FromDateTime(DateTime.Now);
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - record saved with correct expiry calculation
        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ExpDate == today.AddDays(7))), Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Step3_With_Invalid_Date_Shows_Error_And_Stays_On_Step3()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 3,
            ItemName = "Milk",
            BoughtByName = "Alice",
        });

        var localizerMock = new Mock<ILocalizer>();
        localizerMock.Setup(l => l.Get(-100L, "shop.invalid-date"))
            .Returns("Invalid format. Please enter days (e.g. 5) or a variant (e.g. 1 week, 2 months) or click Skip.");

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            CreatePurchaseRepoMock().Object,
            CreatePriceLogRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            localizerMock.Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"invalid text\"}");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - error message sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r =>
                r.Text!.Contains("Invalid format")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - dialog still on step 3
        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(3, state!.Step);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_Step3_With_Cyrillic_Days_Format_Parses_Correctly()
    {
        // Arrange
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 3,
            ItemName = "Сметана",
            StoreName = "Пятёрочка",
            Price = 120.00m,
            BoughtByName = "Alice",
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var groupRepo = CreateGroupRepoMock();

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            CreatePriceLogRepoMock().Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var message = DeserializeMessage(
            "{\"message_id\":10,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100,\"type\":\"supergroup\"},\"text\":\"7 дней\"}");

        // Act
        var today = DateOnly.FromDateTime(DateTime.Now);
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert - record saved with correct expiry calculation from Russian format
        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ExpDate == today.AddDays(7))), Times.Once);
    }

    [Fact]
    public async Task ShopDoneCallbackHandler_Carries_Item_Tags_Into_PriceCaptureDialogState()
    {
        var bot = CreateBotMock();
        var itemRepo = new Mock<ShoppingItemRepository>("Data Source=file:test");
        itemRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new ShoppingItem { Id = 1, GroupId = 10, Name = "Milk", Quantity = "2л", AddedByName = "Alice", Tags = new[] { "Молочка" } });
        itemRepo.Setup(r => r.DeleteAsync(1)).Returns(Task.CompletedTask);

        var listService = new Mock<ShoppingListService>(
            CreateGroupRepoMock().Object,
            new Mock<ShoppingItemRepository>("Data Source=file:test").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
            Mock.Of<ILocalizer>());
        listService.Setup(s => s.BuildListAsync(-100L, It.IsAny<int>(), null))
            .ReturnsAsync(("list text", (InlineKeyboardMarkup?)null, new Group { Id = 10, ChatId = -100L }));

        var groupRepo = CreateGroupRepoMock();
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var purchaseRepo = new Mock<PurchaseHistoryRepository>("Data Source=file:test");
        purchaseRepo.Setup(r => r.GetTopShopsAsync(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());

        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();

        var handler = new ShopDoneCallbackHandler(
            bot.Object,
            itemRepo.Object,
            listService.Object,
            groupRepo.Object,
            historyMock.Object,
            priceDialogService,
            new PendingDialogService<TagCaptureDialogState>(),
            purchaseRepo.Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<ShopDoneCallbackHandler>>());

        var callback = CreateCallback("shop:done:1");

        await handler.HandleAsync(callback, CancellationToken.None);

        var state = priceDialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Contains("Молочка", state!.Tags);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_FinishDialogAsync_LinksTags_When_Present()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        var purchaseRepo = CreatePurchaseRepoMock();

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            CreatePriceLogRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var state = new PriceCaptureDialogState
        {
            ItemName = "Milk",
            BoughtByName = "Alice",
            Tags = new[] { "Молочка" },
        };

        await handler.FinishDialogAsync(-100L, 42L, state, expDate: null, CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p => p.ItemName == "Milk")), Times.Once);
    }

    [Fact]
    public async Task PriceCaptureStepHandler_FinishDialogAsync_With_No_Tags_LeavesRecordUntagged()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        var purchaseRepo = CreatePurchaseRepoMock();

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            CreatePriceLogRepoMock().Object,
            CreateGroupRepoMock().Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateSuggestionService(),
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var state = new PriceCaptureDialogState
        {
            ItemName = "Milk",
            BoughtByName = "Alice",
        };

        await handler.FinishDialogAsync(-100L, 42L, state, expDate: null, CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p => p.ItemName == "Milk")), Times.Once);
    }

    [Fact]
    public async Task PriceSkipCallbackHandler_Skip_Expiry_Saves_Record_With_Tags()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        bot.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var dialogService = new PendingDialogService<PriceCaptureDialogState>();
        dialogService.SetState(-100L, 42L, new PriceCaptureDialogState
        {
            Step = 3,
            ItemName = "Yogurt",
            BoughtByName = "Alice",
            Tags = new[] { "Молочка" },
        });

        var purchaseRepo = CreatePurchaseRepoMock();
        var groupRepo = CreateGroupRepoMock();

        var handler = new PriceSkipCallbackHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            groupRepo.Object, new Mock<TagRepository>("Data Source=file::memory:").Object,
            CreateLocalizerMock().Object,
            Mock.Of<ILogger<PriceSkipCallbackHandler>>());

        var callback = CreateCallback("price:skip_expiry");

        await handler.HandleAsync(callback, CancellationToken.None);

        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p => p.ItemName == "Yogurt")), Times.Once);
    }
}