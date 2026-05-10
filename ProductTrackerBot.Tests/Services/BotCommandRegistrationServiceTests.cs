// <copyright file="BotCommandRegistrationServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ProductTrackerBot.Tests.Services;

using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Xunit;

public class BotCommandRegistrationServiceTests
{
    [Fact]
    public async Task StartAsync_DoesNotThrow_WhenHandlersHaveDescriptions()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var loggerMock = new Mock<ILogger<BotCommandRegistrationService>>();

        botMock
            .Setup(b => b.SendRequest(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handlers = new List<ICommandHandler>
        {
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/buy" &&
                h.Description == "Start shopping session"),
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/list" &&
                h.Description == "View shopping list"),
        };

        var service = new BotCommandRegistrationService(botMock.Object, handlers, loggerMock.Object);

        await service.StartAsync(CancellationToken.None);

        botMock.Verify(
            b => b.SendRequest(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_SkipsSendRequest_WhenNoHandlerHasDescription()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var loggerMock = new Mock<ILogger<BotCommandRegistrationService>>();

        var handlers = new List<ICommandHandler>
        {
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/start" &&
                h.Description == null),
        };

        var service = new BotCommandRegistrationService(botMock.Object, handlers, loggerMock.Object);

        await service.StartAsync(CancellationToken.None);

        botMock.Verify(
            b => b.SendRequest(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_LogsWarningAndDoesNotThrow_WhenSendRequestFails()
    {
        var botMock = new Mock<ITelegramBotClient>();
        var loggerMock = new Mock<ILogger<BotCommandRegistrationService>>();

        botMock
            .Setup(b => b.SendRequest(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        var handlers = new List<ICommandHandler>
        {
            Mock.Of<ICommandHandler>(h =>
                h.Command == "/buy" &&
                h.Description == "Start shopping session"),
        };

        var service = new BotCommandRegistrationService(botMock.Object, handlers, loggerMock.Object);

        // Should not throw
        await service.StartAsync(CancellationToken.None);

        loggerMock.Verify(
            l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
