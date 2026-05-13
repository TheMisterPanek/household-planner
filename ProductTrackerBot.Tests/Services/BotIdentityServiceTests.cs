using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Services;

public class BotIdentityServiceTests
{
    [Fact]
    public async Task StartAsync_StoresBotUsername()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock
            .Setup(b => b.SendRequest(It.IsAny<GetMeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, IsBot = true, FirstName = "TestBot", Username = "mytestbot" });

        var service = new BotIdentityService(botMock.Object, Mock.Of<ILogger<BotIdentityService>>());
        Assert.Equal(string.Empty, service.BotUsername);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal("mytestbot", service.BotUsername);
    }

    [Fact]
    public async Task StartAsync_HandlesNullUsername()
    {
        var botMock = new Mock<ITelegramBotClient>();
        botMock
            .Setup(b => b.SendRequest(It.IsAny<GetMeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, IsBot = true, FirstName = "TestBot", Username = null });

        var service = new BotIdentityService(botMock.Object, Mock.Of<ILogger<BotIdentityService>>());

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(string.Empty, service.BotUsername);
    }

    [Fact]
    public async Task StopAsync_CompletesSynchronously()
    {
        var service = new BotIdentityService(Mock.Of<ITelegramBotClient>(), Mock.Of<ILogger<BotIdentityService>>());
        await service.StopAsync(CancellationToken.None); // should not throw
    }
}
