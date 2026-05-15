using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Database;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Integration;

public abstract class TelegramIntegrationTestBase : IDisposable
{
    protected const string ConnectionString = "Data Source=file:integration_test?mode=memory&cache=shared";

    protected static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly SqliteConnection connection;
    private readonly UpdateDispatcher dispatcher;

    protected Mock<ITelegramBotClient> BotMock { get; }

    protected GroupRepository GroupRepository { get; }

    protected ShoppingItemRepository ItemRepository { get; }

    protected IHistoryRepository HistoryRepository { get; }

    protected PurchaseHistoryRepository PurchaseRepository { get; }

    protected PriceLogRepository PriceLogRepository { get; }

    protected MealRepository MealRepository { get; }

    protected MealIngredientRepository MealIngredientRepository { get; }

    protected MealStepRepository MealStepRepository { get; }

    protected Mock<IAiQueryService> AiQueryServiceMock { get; }

    protected TelegramIntegrationTestBase()
    {
        this.connection = new SqliteConnection(ConnectionString);
        this.connection.Open();

        var dbInitializer = new DatabaseInitializer(ConnectionString, Mock.Of<ILogger<DatabaseInitializer>>());
        dbInitializer.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        this.GroupRepository = new GroupRepository(ConnectionString);
        this.ItemRepository = new ShoppingItemRepository(ConnectionString);
        this.HistoryRepository = new HistoryRepository(ConnectionString);
        this.PurchaseRepository = new PurchaseHistoryRepository(ConnectionString);
        this.PriceLogRepository = new PriceLogRepository(ConnectionString);
        this.MealRepository = new MealRepository(ConnectionString);
        this.MealIngredientRepository = new MealIngredientRepository(ConnectionString);
        this.MealStepRepository = new MealStepRepository(ConnectionString);

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.item-added"))
            .Returns("{name} added {item}");
        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.item-added-quantity"))
            .Returns("{name} added {item} ({quantity})");

        this.BotMock = new Mock<ITelegramBotClient>();
        this.BotMock.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        this.BotMock.Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        this.BotMock.Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.BotMock.Setup(b => b.SendRequest(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.BotMock.Setup(b => b.SendRequest(It.IsAny<EditMessageReplyMarkupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        this.BotMock.Setup(b => b.SendRequest(It.IsAny<SendChatActionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        this.AiQueryServiceMock = new Mock<IAiQueryService>();
        this.AiQueryServiceMock
            .Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("AI response");

        var preferenceRepo = new Mock<IPreferenceRepository>();
        preferenceRepo.Setup(r => r.GetLanguageAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        preferenceRepo.Setup(r => r.SaveLanguageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buyDialogService = new PendingDialogService<BuyDialogState>();
        var priceDialogService = new PendingDialogService<PriceCaptureDialogState>();
        var mealCreateDialogService = new PendingDialogService<MealCreateDialogState>();
        var mealIngredientDialogService = new PendingDialogService<MealAddIngredientDialogState>();
        var mealStepDialogService = new PendingDialogService<MealAddStepDialogState>();

        var listService = new ShoppingListService(this.GroupRepository, this.ItemRepository, localizer.Object);
        var undoService = new UndoService(this.HistoryRepository, this.ItemRepository, this.GroupRepository, Mock.Of<ILogger<UndoService>>());
        var mealMergeService = new MealMergeService();

        // Command handlers
        var buyHandler = new BuyCommandHandler(
            this.BotMock.Object, this.GroupRepository, this.ItemRepository, buyDialogService,
            this.HistoryRepository, localizer.Object, Mock.Of<ILogger<BuyCommandHandler>>());

        var listHandler = new ListCommandHandler(
            this.BotMock.Object, listService, this.GroupRepository, this.HistoryRepository,
            Mock.Of<ILogger<ListCommandHandler>>());

        var historyHandler = new HistoryCommandHandler(this.BotMock.Object, this.HistoryRepository);

        var searchHandler = new SearchCommandHandler(
            this.BotMock.Object, this.PurchaseRepository, this.GroupRepository,
            Mock.Of<ILogger<SearchCommandHandler>>());

        var pricesHandler = new PricesCommandHandler(
            this.BotMock.Object, this.PriceLogRepository, this.GroupRepository, localizer.Object);

        var settingsHandler = new SettingsCommandHandler(
            this.BotMock.Object, localizer.Object, Mock.Of<ILogger<SettingsCommandHandler>>());

        var languageHandler = new LanguageCommandHandler(this.BotMock.Object, localizer.Object);

        var undoCommandHandler = new UndoCommandHandler(this.BotMock.Object, undoService, localizer.Object);

        var mealsHandler = new MealsCommandHandler(
            this.BotMock.Object, this.GroupRepository, this.MealRepository,
            Mock.Of<ILogger<MealsCommandHandler>>());

        var aiHandler = new AiCommandHandler(
            this.BotMock.Object, this.GroupRepository, this.AiQueryServiceMock.Object,
            localizer.Object, Mock.Of<ILogger<AiCommandHandler>>());

        var nonStartHandlers = new List<ICommandHandler>
        {
            buyHandler, listHandler, historyHandler, searchHandler, pricesHandler,
            settingsHandler, languageHandler, undoCommandHandler, mealsHandler, aiHandler,
        };

        var scopeFactory = BuildScopeFactory(nonStartHandlers);
        var startHandler = new StartCommandHandler(scopeFactory, this.BotMock.Object, localizer.Object);

        var commandHandlers = new List<ICommandHandler> { startHandler };
        commandHandlers.AddRange(nonStartHandlers);

        // Callback handlers
        var shopDoneHandler = new ShopDoneCallbackHandler(
            this.BotMock.Object, this.ItemRepository, listService, this.GroupRepository,
            this.HistoryRepository, priceDialogService, this.PurchaseRepository,
            localizer.Object, Mock.Of<ILogger<ShopDoneCallbackHandler>>());

        var shopRemoveHandler = new ShopRemoveCallbackHandler(
            this.BotMock.Object, this.ItemRepository, listService, this.GroupRepository,
            this.HistoryRepository, Mock.Of<ILogger<ShopRemoveCallbackHandler>>());

        var actionCancelHandler = new ActionCancelCallbackHandler(
            this.BotMock.Object, Mock.Of<ILogger<ActionCancelCallbackHandler>>());

        var langCallbackHandler = new LanguageCallbackHandler(
            this.BotMock.Object, this.GroupRepository, localizer.Object);

        var langSelectionHandler = new LanguageSelectionHandler(
            this.BotMock.Object, preferenceRepo.Object, this.HistoryRepository,
            localizer.Object, Mock.Of<ILogger<LanguageSelectionHandler>>());

        var buySkipHandler = new BuySkipCallbackHandler(
            this.BotMock.Object, buyDialogService, this.ItemRepository, this.HistoryRepository,
            localizer.Object, Mock.Of<ILogger<BuySkipCallbackHandler>>());

        var buySkipExpiryHandler = new BuySkipExpiryCallbackHandler(
            this.BotMock.Object, buyDialogService, this.ItemRepository, this.HistoryRepository,
            localizer.Object, Mock.Of<ILogger<BuySkipExpiryCallbackHandler>>());

        var listNextHandler = new ListNextCallbackHandler(
            this.BotMock.Object, listService, this.GroupRepository, this.HistoryRepository,
            Mock.Of<ILogger<ListNextCallbackHandler>>());

        var listPrevHandler = new ListPrevCallbackHandler(
            this.BotMock.Object, listService, this.GroupRepository, this.HistoryRepository,
            Mock.Of<ILogger<ListPrevCallbackHandler>>());

        var undoInlineHandler = new UndoInlineCallbackHandler(
            this.BotMock.Object, undoService, priceDialogService, localizer.Object,
            Mock.Of<ILogger<UndoInlineCallbackHandler>>());

        var priceSkipHandler = new PriceSkipCallbackHandler(
            this.BotMock.Object, priceDialogService, this.PurchaseRepository, this.GroupRepository,
            localizer.Object, Mock.Of<ILogger<PriceSkipCallbackHandler>>());

        var priceShopHandler = new PriceShopSuggestionCallbackHandler(
            this.BotMock.Object, priceDialogService, Mock.Of<ILogger<PriceShopSuggestionCallbackHandler>>());

        var mealCallbackHandler = new MealCallbackHandler(
            this.BotMock.Object, this.GroupRepository, this.MealRepository,
            this.MealIngredientRepository, this.MealStepRepository, this.ItemRepository,
            mealCreateDialogService, mealIngredientDialogService, mealStepDialogService,
            mealMergeService, localizer.Object, Mock.Of<ILogger<MealCallbackHandler>>());

        var callbackHandlers = new List<ICallbackHandler>
        {
            shopDoneHandler, shopRemoveHandler, actionCancelHandler, langCallbackHandler,
            langSelectionHandler, buySkipHandler, buySkipExpiryHandler, listNextHandler,
            listPrevHandler, undoInlineHandler, priceSkipHandler, priceShopHandler,
            mealCallbackHandler,
        };

        // Dialog handlers
        var buyStepHandler = new BuyStepHandler(
            this.BotMock.Object, buyDialogService, this.ItemRepository, this.HistoryRepository,
            localizer.Object, Mock.Of<ILogger<BuyStepHandler>>());

        var priceCaptureHandler = new PriceCaptureStepHandler(
            this.BotMock.Object, priceDialogService, this.PurchaseRepository, this.PriceLogRepository,
            this.GroupRepository, localizer.Object, Mock.Of<ILogger<PriceCaptureStepHandler>>());

        var mealDialogHandler = new MealDialogStepHandler(
            this.BotMock.Object, this.GroupRepository, mealCreateDialogService,
            mealIngredientDialogService, mealStepDialogService,
            this.MealRepository, this.MealIngredientRepository, this.MealStepRepository,
            Mock.Of<ILogger<MealDialogStepHandler>>());

        var dialogHandlers = new List<IDialogMessageHandler>
        {
            buyStepHandler, priceCaptureHandler, mealDialogHandler,
        };

        this.dispatcher = new UpdateDispatcher(
            commandHandlers, callbackHandlers, dialogHandlers,
            Mock.Of<ILogger<UpdateDispatcher>>());
    }

    protected Task DispatchAsync(Update update) =>
        this.dispatcher.HandleUpdateAsync(this.BotMock.Object, update, CancellationToken.None);

    protected async Task ClearDataAsync()
    {
        await using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM PriceLog;
            DELETE FROM PurchaseHistory;
            DELETE FROM BotActionHistory;
            DELETE FROM MealSteps;
            DELETE FROM MealIngredients;
            DELETE FROM Meals;
            DELETE FROM ShoppingItems;
            DELETE FROM Groups;";
        await cmd.ExecuteNonQueryAsync();
    }

    // Chat type is included so handlers that check ChatType (e.g. /meals, /prices) work in supergroup mode
    protected static Update CommandUpdate(long chatId, long userId, string text) =>
        DeserializeUpdate(
            $"{{\"update_id\":1,\"message\":{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"TestUser\"}},\"chat\":{{\"id\":{chatId},\"type\":\"supergroup\"}},\"text\":\"{text}\"}}}}");

    protected static Update CallbackUpdate(long chatId, long userId, int messageId, string data) =>
        DeserializeUpdate(
            $"{{\"update_id\":1,\"callback_query\":{{\"id\":\"cb1\",\"from\":{{\"id\":{userId},\"first_name\":\"TestUser\"}},\"message\":{{\"message_id\":{messageId},\"chat\":{{\"id\":{chatId},\"type\":\"supergroup\"}}}},\"data\":\"{data}\"}}}}");

    protected static Update MessageUpdate(long chatId, long userId, string text) =>
        DeserializeUpdate(
            $"{{\"update_id\":1,\"message\":{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"TestUser\"}},\"chat\":{{\"id\":{chatId},\"type\":\"supergroup\"}},\"text\":\"{text}\"}}}}");

    // Private chat variant for handlers that restrict to group-only
    protected static Update PrivateCommandUpdate(long chatId, long userId, string text) =>
        DeserializeUpdate(
            $"{{\"update_id\":1,\"message\":{{\"message_id\":1,\"from\":{{\"id\":{userId},\"first_name\":\"TestUser\"}},\"chat\":{{\"id\":{chatId},\"type\":\"private\"}},\"text\":\"{text}\"}}}}");

    public void Dispose() => this.connection.Dispose();

    private static Update DeserializeUpdate(string json) =>
        JsonSerializer.Deserialize<Update>(json, JsonOpts)!;

    private static IServiceScopeFactory BuildScopeFactory(IEnumerable<ICommandHandler> handlers)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<ICommandHandler>)))
            .Returns(handlers);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return scopeFactory.Object;
    }
}
