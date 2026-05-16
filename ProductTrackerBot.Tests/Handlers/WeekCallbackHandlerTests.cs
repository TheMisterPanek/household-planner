namespace ProductTrackerBot.Tests.Handlers;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

public class WeekCallbackHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery CreateCallback(string data, long chatId = -100L, int messageId = 99)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = "cb1",
            from = new { id = 42, first_name = "Alice" },
            message = new { message_id = messageId, chat = new { id = chatId, type = "group" } },
            data,
        }, JsonOpts);
        return JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;
    }

    private static (Mock<ITelegramBotClient> Bot, List<string> SentTexts, List<string> EditedTexts) CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var sentTexts = new List<string>();
        var editedTexts = new List<string>();

        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sentTexts.Add(smr.Text);
            })
            .ReturnsAsync(new Message());

        botMock
            .Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is EditMessageTextRequest emr) editedTexts.Add(emr.Text);
            })
            .ReturnsAsync(new Message());

        botMock
            .Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return (botMock, sentTexts, editedTexts);
    }

    private static WeekCallbackHandler CreateHandler(
        Mock<ITelegramBotClient> bot,
        GroupRepository? groupRepo = null,
        DayMealsRepository? dayMealsRepo = null,
        MealRepository? mealRepo = null,
        MealIngredientRepository? ingredientRepo = null,
        ShoppingItemRepository? shoppingRepo = null,
        ILocalizer? localizer = null)
    {
        var gr = groupRepo ?? new Mock<GroupRepository>(MockBehavior.Default, "cs").Object;
        var dm = dayMealsRepo ?? new Mock<DayMealsRepository>(MockBehavior.Default, "cs").Object;
        var mr = mealRepo ?? new Mock<MealRepository>(MockBehavior.Default, "cs").Object;
        var ir = ingredientRepo ?? new Mock<MealIngredientRepository>(MockBehavior.Default, "cs").Object;
        var sr = shoppingRepo ?? new Mock<ShoppingItemRepository>(MockBehavior.Default, "cs").Object;
        var loc = localizer ?? Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), It.IsAny<string>()) == "key");

        return new WeekCallbackHandler(
            bot.Object, gr, dm, mr, ir, sr, loc,
            Mock.Of<ILogger<WeekCallbackHandler>>());
    }

    [Fact]
    public async Task Day_Shows_Day_Detail_View()
    {
        var (bot, _, editedTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1)).ReturnsAsync(new List<DayMealEntry>
        {
            new() { DayOfWeek = 1, MealId = 1, MealName = "Pasta" },
        });

        var mealRepo = new Mock<MealRepository>("cs");
        mealRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<Meal>
        {
            new() { Id = 1, GroupId = 1, Name = "Pasta" },
        });

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.day.1") == "Mon" &&
            l.Get(It.IsAny<long>(), "week.btn-clear") == "X" &&
            l.Get(It.IsAny<long>(), "week.btn-add-meal") == "+ Add" &&
            l.Get(It.IsAny<long>(), "week.btn-back") == "Back" &&
            l.Get(It.IsAny<long>(), "week.btn-to-cart") == "Cart");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, mealRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:day:1"), CancellationToken.None);

        Assert.Single(editedTexts);
        Assert.Contains("Mon", editedTexts[0]);
    }

    [Fact]
    public async Task Pick_Shows_Meal_Picker()
    {
        var (bot, _, editedTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var mealRepo = new Mock<MealRepository>("cs");
        mealRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<Meal>
        {
            new() { Id = 1, GroupId = 1, Name = "Pasta" },
            new() { Id = 2, GroupId = 1, Name = "Pizza" },
        });

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.pick-prompt") == "Pick for {0}:" &&
            l.Get(It.IsAny<long>(), "week.btn-back") == "Back" &&
            l.Get(It.IsAny<long>(), "week.day.1") == "Mon");

        var handler = CreateHandler(bot, groupRepo.Object, mealRepo: mealRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:pick:1"), CancellationToken.None);

        Assert.Single(editedTexts);
        Assert.Contains("Mon", editedTexts[0]);
    }

    [Fact]
    public async Task Pick_With_No_Meals_Answers_Callback_With_NoMeals_Message()
    {
        var (bot, _, _) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var mealRepo = new Mock<MealRepository>("cs");
        mealRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<Meal>());

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.no-meals") == "No meals yet");

        var handler = CreateHandler(bot, groupRepo.Object, mealRepo: mealRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:pick:1"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "No meals yet"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Assign_Inserts_And_Shows_Day_Detail()
    {
        var (bot, _, editedTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetCountAsync(1, 3)).ReturnsAsync(0);
        dayMealsRepo.Setup(r => r.InsertAsync(1, 3, 42)).Returns(Task.CompletedTask);
        dayMealsRepo.Setup(r => r.GetWeekAsync(1)).ReturnsAsync(new List<DayMealEntry>
        {
            new() { DayOfWeek = 3, MealId = 42, MealName = "Pasta" },
        });

        var mealRepo = new Mock<MealRepository>("cs");
        mealRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<Meal>
        {
            new() { Id = 42, GroupId = 1, Name = "Pasta" },
        });

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.day.3") == "Wed" &&
            l.Get(It.IsAny<long>(), "week.btn-clear") == "X" &&
            l.Get(It.IsAny<long>(), "week.btn-add-meal") == "+ Add" &&
            l.Get(It.IsAny<long>(), "week.btn-back") == "Back" &&
            l.Get(It.IsAny<long>(), "week.btn-to-cart") == "Cart");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, mealRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:assign:3:42"), CancellationToken.None);

        dayMealsRepo.Verify(r => r.InsertAsync(1, 3, 42), Times.Once);
        Assert.Single(editedTexts);
        Assert.Contains("Wed", editedTexts[0]);
    }

    [Fact]
    public async Task Assign_At_Max_Meals_Answers_Callback_With_MaxMeals()
    {
        var (bot, _, editedTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetCountAsync(1, 3)).ReturnsAsync(10);

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.max-meals") == "Max 10 meals");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:assign:3:42"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "Max 10 meals"),
            It.IsAny<CancellationToken>()), Times.Once);
        dayMealsRepo.Verify(r => r.InsertAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        Assert.Empty(editedTexts);
    }

    [Fact]
    public async Task Clear_Removes_Specific_Meal_And_Shows_Day_Detail()
    {
        var (bot, _, editedTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.ClearMealAsync(1, 5, 42)).Returns(Task.CompletedTask);
        dayMealsRepo.Setup(r => r.GetWeekAsync(1)).ReturnsAsync(new List<DayMealEntry>());

        var mealRepo = new Mock<MealRepository>("cs");
        mealRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<Meal>());

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.day.5") == "Fri" &&
            l.Get(It.IsAny<long>(), "week.btn-add-meal") == "+ Add" &&
            l.Get(It.IsAny<long>(), "week.btn-back") == "Back" &&
            l.Get(It.IsAny<long>(), "week.btn-to-cart") == "Cart");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, mealRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:clear:5:42"), CancellationToken.None);

        dayMealsRepo.Verify(r => r.ClearMealAsync(1, 5, 42), Times.Once);
        Assert.Single(editedTexts);
        Assert.Contains("Fri", editedTexts[0]);
    }

    [Fact]
    public async Task Back_Renders_Day_List_View()
    {
        var (bot, _, editedTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1)).ReturnsAsync(new List<DayMealEntry>());

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.header") == "Plan:" &&
            l.Get(It.IsAny<long>(), "week.day.1") == "Mon" &&
            l.Get(It.IsAny<long>(), "week.day.2") == "Tue" &&
            l.Get(It.IsAny<long>(), "week.day.3") == "Wed" &&
            l.Get(It.IsAny<long>(), "week.day.4") == "Thu" &&
            l.Get(It.IsAny<long>(), "week.day.5") == "Fri" &&
            l.Get(It.IsAny<long>(), "week.day.6") == "Sat" &&
            l.Get(It.IsAny<long>(), "week.day.7") == "Sun");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:back"), CancellationToken.None);

        Assert.Single(editedTexts);
        Assert.StartsWith("Plan:", editedTexts[0]);
        Assert.Contains("Mon: —", editedTexts[0]);
        Assert.Contains("Sun: —", editedTexts[0]);
    }

    [Fact]
    public async Task ToCart_With_No_Meals_Answers_With_NoMeals()
    {
        var (bot, _, _) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1)).ReturnsAsync(new List<DayMealEntry>());

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.cart-no-meals") == "No meals");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:to_cart:1"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "No meals"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToCart_With_Meals_But_No_Ingredients_Answers_With_NoIngredients()
    {
        var (bot, _, _) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1)).ReturnsAsync(new List<DayMealEntry>
        {
            new() { DayOfWeek = 1, MealId = 1, MealName = "Pasta" },
        });

        var ingredientRepo = new Mock<MealIngredientRepository>("cs");
        ingredientRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<MealIngredient>());

        var shoppingRepo = new Mock<ShoppingItemRepository>("cs");
        shoppingRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<ShoppingItem>());

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.cart-no-ingredients") == "No ingredients");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, ingredientRepo: ingredientRepo.Object, shoppingRepo: shoppingRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:to_cart:1"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "No ingredients"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToCart_Adds_Ingredients_Answers_With_Count()
    {
        var (bot, _, _) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var dayMealsRepo = new Mock<DayMealsRepository>("cs");
        dayMealsRepo.Setup(r => r.GetWeekAsync(1)).ReturnsAsync(new List<DayMealEntry>
        {
            new() { DayOfWeek = 1, MealId = 1, MealName = "Pasta" },
        });

        var ingredientRepo = new Mock<MealIngredientRepository>("cs");
        ingredientRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<MealIngredient>
        {
            new() { Id = 1, MealId = 1, Name = "Flour", Quantity = "500g" },
            new() { Id = 2, MealId = 1, Name = "Eggs", Quantity = "3" },
        });

        var shoppingRepo = new Mock<ShoppingItemRepository>("cs");
        shoppingRepo.Setup(r => r.GetAllAsync(1)).ReturnsAsync(new List<ShoppingItem>());
        shoppingRepo.Setup(r => r.AddAsync(1, "Flour", "500g", "Week", default)).ReturnsAsync(new ShoppingItem { Id = 1 });
        shoppingRepo.Setup(r => r.AddAsync(1, "Eggs", "3", "Week", default)).ReturnsAsync(new ShoppingItem { Id = 2 });

        var localizer = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "week.cart-added") == "Added {0} items");

        var handler = CreateHandler(bot, groupRepo.Object, dayMealsRepo.Object, ingredientRepo: ingredientRepo.Object, shoppingRepo: shoppingRepo.Object, localizer: localizer);
        await handler.HandleAsync(CreateCallback("week:to_cart:1"), CancellationToken.None);

        shoppingRepo.Verify(r => r.AddAsync(1, "Flour", "500g", "Week", default), Times.Once);
        shoppingRepo.Verify(r => r.AddAsync(1, "Eggs", "3", "Week", default), Times.Once);
        bot.Verify(b => b.SendRequest(
            It.Is<AnswerCallbackQueryRequest>(r => r.Text == "Added 2 items"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Noop_Only_Answers_Callback()
    {
        var (bot, _, editedTexts) = CreateBotMock();
        var groupRepo = new Mock<GroupRepository>(MockBehavior.Default, "cs");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L)).ReturnsAsync(new Group { Id = 1, ChatId = -100 });

        var handler = CreateHandler(bot, groupRepo.Object);
        await handler.HandleAsync(CreateCallback("week:noop"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(
            It.IsAny<AnswerCallbackQueryRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(editedTexts);
    }
}
