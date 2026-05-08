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
        return repo;
    }

    private static Mock<GroupRepository> CreateGroupRepoMock()
    {
        var repo = new Mock<GroupRepository>("Data Source=file:test");
        repo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync((long chatId) => new Group { Id = 10, ChatId = chatId });
        return repo;
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
            Mock.Of<ILocalizer>());
        listService.Setup(s => s.BuildListAsync(-100L, It.IsAny<int>()))
            .ReturnsAsync(("list text", (InlineKeyboardMarkup?)null, new Group { Id = 10, ChatId = -100L }));

        var groupRepo = new Mock<GroupRepository>("Data Source=file:test");
        groupRepo.Setup(r => r.UpdateListMessageIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var historyMock = new Mock<IHistoryRepository>();
        historyMock.Setup(h => h.RecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<BotActionType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();

        var handler = new ShopDoneCallbackHandler(
            bot.Object,
            itemRepo.Object,
            listService.Object,
            groupRepo.Object,
            historyMock.Object,
            priceDialogService,
            Mock.Of<ILocalizer>(),
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
                r.Text!.Contains("📍 Where did you buy Milk") &&
                r.ReplyMarkup != null),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - item was deleted
        itemRepo.Verify(r => r.DeleteAsync(1), Times.Once);
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
            CreateGroupRepoMock().Object,
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
                r.Text!.Contains("💰 Price for Milk") &&
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
        var groupRepo = CreateGroupRepoMock();

        var handler = new PriceCaptureStepHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            groupRepo.Object,
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
            It.Is<SendMessageRequest>(r => r.Text!.Contains("✓ Milk recorded")),
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
            CreateGroupRepoMock().Object,
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
            It.Is<SendMessageRequest>(r => r.Text!.Contains("That doesn't look like a price")),
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
            CreateGroupRepoMock().Object);

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
            It.Is<SendMessageRequest>(r => r.Text!.Contains("💰 Price for Milk")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceSkipCallbackHandler_Skip_Price_Saves_Record_With_Null_Price_And_Clears_Dialog()
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

        var handler = new PriceSkipCallbackHandler(
            bot.Object,
            dialogService,
            purchaseRepo.Object,
            groupRepo.Object);

        var callback = CreateCallback("price:skip_price");

        // Act
        await handler.HandleAsync(callback, CancellationToken.None);

        // Assert - record saved with null price
        purchaseRepo.Verify(r => r.AddAsync(It.Is<PurchaseRecord>(p =>
            p.ItemName == "Milk" &&
            p.Price == null &&
            p.StoreName == "Magnit" &&
            p.BoughtByName == "Alice" &&
            p.GroupId == 10)), Times.Once);

        // Assert - dialog cleared
        Assert.Null(dialogService.GetState(-100L, 42L));

        // Assert - confirmation sent
        bot.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text!.Contains("✓ Milk recorded")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}