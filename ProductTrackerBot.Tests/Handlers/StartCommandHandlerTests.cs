// <copyright file="StartCommandHandlerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Tests.Handlers;

using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Xunit;

public class StartCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

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

        return (botMock, sentTexts);
    }

    private static Message CreateMessage(string text, long chatId = 123L)
    {
        var json = JsonSerializer.Serialize(new
        {
            message_id = 1,
            from = new { id = 456, first_name = "Test" },
            chat = new { id = chatId, type = "private" },
            text,
        }, JsonOpts);
        return JsonSerializer.Deserialize<Message>(json, JsonOpts)!;
    }

    [Fact]
    public async Task HandleAsync_RepliesWithWelcomeMessageAndCommandList_WhenHandlersHaveDescriptions()
    {
        var (bot, sentTexts) = CreateBotMock();
        var localizerMock = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "start.welcome") == "Welcome! Available commands:");

        var handlers = new List<ICommandHandler>
        {
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/buy" &&
                h.Description == "Start shopping session"),
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/list" &&
                h.Description == "View shopping list"),
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/start" &&
                h.Description == null),
        };

        var handler = new StartCommandHandler(handlers, bot.Object, localizerMock);

        await handler.HandleAsync(CreateMessage("/start"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Contains("Welcome! Available commands:", sentTexts[0]);
        Assert.Contains("/buy — Start shopping session", sentTexts[0]);
        Assert.Contains("/list — View shopping list", sentTexts[0]);
        Assert.DoesNotContain("/start", sentTexts[0]);
    }

    [Fact]
    public async Task HandleAsync_RepliesWithNoCommandsMessage_WhenNoHandlerHasDescription()
    {
        var (bot, sentTexts) = CreateBotMock();
        var localizerMock = Mock.Of<ILocalizer>(l =>
            l.Get(It.IsAny<long>(), "start.no-commands") == "No commands available.");

        var handlers = new List<ICommandHandler>
        {
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/start" &&
                h.Description == null),
        };

        var handler = new StartCommandHandler(handlers, bot.Object, localizerMock);

        await handler.HandleAsync(CreateMessage("/start"), CancellationToken.None);

        Assert.Single(sentTexts);
        Assert.Equal("No commands available.", sentTexts[0]);
    }

    [Fact]
    public void Description_IsNull_SoStartIsExcludedFromItsOwnList()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var localizerMock = new Mock<ILocalizer>();

        var handler = new StartCommandHandler(
            new List<ICommandHandler>(),
            botMock.Object,
            localizerMock.Object);

        Assert.Null(handler.Description);
        Assert.Equal("/start", handler.Command);
    }
}
