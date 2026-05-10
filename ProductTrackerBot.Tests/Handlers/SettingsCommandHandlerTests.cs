using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class SettingsCommandHandlerTests
{
    private readonly Mock<ITelegramBotClient> mockBotClient;
    private readonly Mock<ILocalizer> mockLocalizer;
    private readonly Mock<ILogger<SettingsCommandHandler>> mockLogger;
    private readonly SettingsCommandHandler handler;

    public SettingsCommandHandlerTests()
    {
        this.mockBotClient = new Mock<ITelegramBotClient>();
        this.mockLocalizer = new Mock<ILocalizer>();
        this.mockLogger = new Mock<ILogger<SettingsCommandHandler>>();
        this.handler = new SettingsCommandHandler(this.mockBotClient.Object, this.mockLocalizer.Object, this.mockLogger.Object);
    }

    [Fact]
    public void Command_Returns_Settings()
    {
        Assert.Equal("/settings", this.handler.Command);
    }

    [Fact]
    public async Task HandleAsync_In_PrivateChat_Shows_Settings_Menu()
    {
        var chatId = 123L;
        var userId = 123L;

        var message = new Message
        {
            Chat = new Chat { Id = chatId },
            From = new User { Id = userId, FirstName = "Test" },
        };

        this.mockLocalizer
            .Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns("translated");

        await this.handler.HandleAsync(message, CancellationToken.None);

        this.mockLocalizer.Verify(
            l => l.Get(chatId, "menu_prompt"),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_In_GroupChat_Shows_Settings_Menu()
    {
        var chatId = -100456L;
        var userId = 789L;

        var message = new Message
        {
            Chat = new Chat { Id = chatId },
            From = new User { Id = userId, FirstName = "Test" },
        };

        this.mockLocalizer
            .Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns("translated");

        await this.handler.HandleAsync(message, CancellationToken.None);

        // Verify that settings menu is shown for the group
        this.mockLocalizer.Verify(
            l => l.Get(chatId, "menu_prompt"),
            Times.Once);
    }
}
