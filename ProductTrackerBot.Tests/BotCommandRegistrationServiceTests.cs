// <copyright file="BotCommandRegistrationServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Tests;

using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot;
using ProductTrackerBot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Xunit;

public class BotCommandRegistrationServiceTests
{
    private static (Mock<ITelegramBotClient> Bot, List<SetMyCommandsRequest> Requests) CreateBotMock()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var requests = new List<SetMyCommandsRequest>();

        botMock
            .Setup(b => b.SendRequest(It.IsAny<SetMyCommandsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<bool>, CancellationToken>((req, _) =>
            {
                if (req is SetMyCommandsRequest r) requests.Add(r);
            })
            .ReturnsAsync(true);

        return (botMock, requests);
    }

    [Fact]
    public async Task StartAsync_CallsSetMyCommands_WhenHandlersHaveDescriptions()
    {
        var (bot, requests) = CreateBotMock();
        var loggerMock = new Mock<ILogger<BotCommandRegistrationService>>();

        var handlers = new List<ICommandHandler>
        {
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/buy" &&
                h.Description == "Start shopping session"),
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/list" &&
                h.Description == "View shopping list"),
        };

        var service = new BotCommandRegistrationService(bot.Object, handlers, loggerMock.Object);

        await service.StartAsync(CancellationToken.None);

        Assert.Single(requests);
        var cmds = requests[0].Commands.ToList();
        Assert.Equal(2, cmds.Count);
        Assert.Contains(cmds, c => c.Command == "buy" && c.Description == "Start shopping session");
        Assert.Contains(cmds, c => c.Command == "list" && c.Description == "View shopping list");
    }

    [Fact]
    public async Task StartAsync_SkipsSetMyCommands_WhenNoHandlerHasDescription()
    {
        var (bot, requests) = CreateBotMock();
        var loggerMock = new Mock<ILogger<BotCommandRegistrationService>>();

        var handlers = new List<ICommandHandler>
        {
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/start" &&
                h.Description == null),
        };

        var service = new BotCommandRegistrationService(bot.Object, handlers, loggerMock.Object);

        await service.StartAsync(CancellationToken.None);

        Assert.Empty(requests);
    }

    [Fact]
    public async Task StartAsync_LogsWarning_AndDoesNotThrow_WhenSetMyCommandsThrows()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock
            .Setup(b => b.SendRequest(It.IsAny<SetMyCommandsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));

        var loggerMock = new Mock<ILogger<BotCommandRegistrationService>>();

        var handlers = new List<ICommandHandler>
        {
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/buy" &&
                h.Description == "Start shopping session"),
        };

        var service = new BotCommandRegistrationService(botMock.Object, handlers, loggerMock.Object);

        await service.StartAsync(CancellationToken.None);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
