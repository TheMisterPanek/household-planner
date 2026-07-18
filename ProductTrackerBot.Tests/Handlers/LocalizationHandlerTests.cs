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
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProductTrackerBot.Tests.Handlers;

public class LocalizationHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static CallbackQuery DeserializeCallbackQuery(string json) =>
        JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;

    private static (Mock<ITelegramBotClient> Bot, List<string> SentTexts) CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var sentTexts = new List<string>();

        botMock
            .Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr) sentTexts.Add(smr.Text);
            })
            .ReturnsAsync(new Message());

        botMock
            .Setup(b => b.SendRequest(It.IsAny<AnswerCallbackQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        botMock
            .Setup(b => b.SendRequest(It.IsAny<EditMessageTextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        return (botMock, sentTexts);
    }

    private static Mock<GroupRepository> CreateGroupRepoMock()
    {
        var repo = new Mock<GroupRepository>("Data Source=file:test");
        repo.Setup(r => r.GetOrCreateAsync(It.IsAny<long>()))
            .ReturnsAsync((long chatId) => new Group { Id = 10, ChatId = chatId, LanguageCode = "en" });
        repo.Setup(r => r.SetLanguageAsync(It.IsAny<long>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    private static Mock<ILocalizer> CreateLocalizerMock()
    {
        var localizer = new Mock<ILocalizer>();

        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.group-only"))
            .Returns("This command only works in a group chat.");

        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.what-to-buy"))
            .Returns("What to buy?");

        localizer.Setup(l => l.Get(It.IsAny<long>(), "buy.skip"))
            .Returns("Skip");

        localizer.Setup(l => l.Get(It.IsAny<long>(), "language.select-prompt"))
            .Returns("Select language:");

        localizer.Setup(l => l.Get(It.IsAny<long>(), "language.english"))
            .Returns("English");

        localizer.Setup(l => l.Get(It.IsAny<long>(), "language.russian"))
            .Returns("Русский");

        localizer.Setup(l => l.Get(It.IsAny<long>(), "language.confirmation"))
            .Returns("Language set to {language}");

        return localizer;
    }

    private static Message CreatePrivateMessage(string text, long userId = 42L)
    {
        var json = JsonSerializer.Serialize(new
        {
            message_id = 1,
            from = new { id = userId, first_name = "Alice" },
            chat = new { id = userId, type = "private" },
            text,
        }, JsonOpts);
        return DeserializeMessage(json);
    }

    private static Message CreateGroupMessage(string text, long chatId = -100L, long userId = 42L)
    {
        var json = JsonSerializer.Serialize(new
        {
            message_id = 1,
            from = new { id = userId, first_name = "Alice" },
            chat = new { id = chatId, type = "supergroup" },
            text,
        }, JsonOpts);
        return DeserializeMessage(json);
    }

    private static CallbackQuery CreateCallbackQuery(string data, long chatId = -100L, int messageId = 1)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = "callback_id",
            from = new { id = 42L, first_name = "Alice" },
            chat_instance = "instance",
            data,
            message = new
            {
                message_id = messageId,
                chat = new { id = chatId, type = "supergroup" },
                date = 123456789,
            },
        }, JsonOpts);
        return DeserializeCallbackQuery(json);
    }

    [Fact]
    public async Task BuyCommandHandler_Uses_Localized_WhatToBuy_Prompt_When_No_Args()
    {
        // Arrange
        var (bot, sentTexts) = CreateBotMock();
        var localizer = CreateLocalizerMock();
        var groupRepo = CreateGroupRepoMock();
        var tagRepo = new Mock<TagRepository>("Data Source=file::memory:");
        tagRepo.Setup(r => r.GetTopTagsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<string>());

        var historyRepository = Mock.Of<IHistoryRepository>();
        var tagCaptureService = new TagCaptureService(bot.Object, new PendingDialogService<TagCaptureDialogState>(), new PendingDialogService<PriceCaptureDialogState>(), tagRepo.Object, localizer.Object);
        var buyAddService = new BuyAddService(
            bot.Object,
            new Mock<ShoppingItemRepository>("Data Source=file::memory:").Object,
            historyRepository,
            tagCaptureService,
            localizer.Object,
            Mock.Of<ILogger<BuyAddService>>());

        var handler = new BuyCommandHandler(
            bot.Object,
            groupRepo.Object,
            new PendingDialogService<BuyDialogState>(),
            new PendingAddService(),
            localizer.Object,
            new ShoppingListService(
                new Mock<GroupRepository>("Data Source=file::memory:").Object,
                new Mock<ShoppingItemRepository>("Data Source=file::memory:").Object,
                new Mock<TagRepository>("Data Source=file::memory:").Object,
                localizer.Object),
            historyRepository,
            tagCaptureService,
            buyAddService,
            Mock.Of<ILogger<BuyCommandHandler>>());

        var message = CreateGroupMessage("/buy");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert
        Assert.Single(sentTexts);
        Assert.Equal("What to buy?", sentTexts[0]);
        localizer.Verify(l => l.Get(message.Chat.Id, "buy.what-to-buy"), Times.Once);
    }

    [Fact]
    public async Task LanguageCommandHandler_Sends_Inline_Keyboard_With_Locale_Buttons()
    {
        // Arrange
        var (bot, sentTexts) = CreateBotMock();
        var localizer = CreateLocalizerMock();
        var capturedKeyboard = (InlineKeyboardMarkup?)null;

        bot
            .Setup(b => b.SendRequest(
                It.IsAny<SendMessageRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<IRequest<Message>, CancellationToken>((req, _) =>
            {
                if (req is SendMessageRequest smr)
                {
                    sentTexts.Add(smr.Text);
                    capturedKeyboard = smr.ReplyMarkup as InlineKeyboardMarkup;
                }
            })
            .ReturnsAsync(new Message());

        var handler = new LanguageCommandHandler(bot.Object, localizer.Object);
        var message = CreateGroupMessage("/language");

        // Act
        await handler.HandleAsync(message, CancellationToken.None);

        // Assert
        Assert.Single(sentTexts);
        Assert.Equal("Select language:", sentTexts[0]);
        Assert.NotNull(capturedKeyboard);
        var rows = capturedKeyboard!.InlineKeyboard.ToList();
        Assert.Single(rows); // One row of buttons
        Assert.Equal(2, rows[0].Count()); // Two buttons: English and Russian
    }

    [Fact]
    public async Task LanguageCallbackHandler_Calls_SetLanguageAsync_And_Replies_With_Confirmation()
    {
        // Arrange
        var (bot, sentTexts) = CreateBotMock();
        var localizer = CreateLocalizerMock();
        var groupRepo = CreateGroupRepoMock();

        var handler = new LanguageCallbackHandler(
            bot.Object,
            groupRepo.Object,
            localizer.Object);

        var callbackQuery = CreateCallbackQuery("lang:en", -100L, 1);

        // Act
        await handler.HandleAsync(callbackQuery, CancellationToken.None);

        // Assert
        groupRepo.Verify(g => g.SetLanguageAsync(callbackQuery.Message!.Chat.Id, "en"), Times.Once);
    }
}
