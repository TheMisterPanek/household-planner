using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class LanguageSelectionHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static CallbackQuery DeserializeCallbackQuery(string json) =>
        JsonSerializer.Deserialize<CallbackQuery>(json, JsonOpts)!;

    private static CallbackQuery LanguageCallbackQuery(string languageCode) =>
        DeserializeCallbackQuery(
            $"{{\"id\":\"cb1\",\"from\":{{\"id\":100,\"first_name\":\"Test\"}}," +
            $"\"message\":{{\"message_id\":10,\"chat\":{{\"id\":200}}}}," +
            $"\"data\":\"settings_lang:{languageCode}\"}}");

    [Fact]
    public void CallbackPrefix_Returns_Settings_Lang()
    {
        var mockBotClient = new Mock<ITelegramBotClient>();
        var mockPrefRepo = new Mock<IPreferenceRepository>();
        var mockHistRepo = new Mock<IHistoryRepository>();
        var mockLocalizer = new Mock<ILocalizer>();
        var mockLogger = new Mock<ILogger<LanguageSelectionHandler>>();

        var handler = new LanguageSelectionHandler(
            mockBotClient.Object,
            mockPrefRepo.Object,
            mockHistRepo.Object,
            mockLocalizer.Object,
            mockLogger.Object);

        Assert.Equal("settings_lang", handler.CallbackPrefix);
    }

    [Fact]
    public async Task HandleAsync_Saves_Language_Preference()
    {
        var mockBotClient = new Mock<ITelegramBotClient>();
        var mockPrefRepo = new Mock<IPreferenceRepository>();
        var mockHistRepo = new Mock<IHistoryRepository>();
        var mockLocalizer = new Mock<ILocalizer>();
        var mockLogger = new Mock<ILogger<LanguageSelectionHandler>>();

        mockLocalizer
            .Setup(l => l.Get(200L, "language_saved"))
            .Returns("Language saved");

        var handler = new LanguageSelectionHandler(
            mockBotClient.Object,
            mockPrefRepo.Object,
            mockHistRepo.Object,
            mockLocalizer.Object,
            mockLogger.Object);

        var callbackQuery = LanguageCallbackQuery("en");

        await handler.HandleAsync(callbackQuery, CancellationToken.None);

        mockPrefRepo.Verify(r => r.SaveLanguageAsync(200L, "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Records_History()
    {
        var mockBotClient = new Mock<ITelegramBotClient>();
        var mockPrefRepo = new Mock<IPreferenceRepository>();
        var mockHistRepo = new Mock<IHistoryRepository>();
        var mockLocalizer = new Mock<ILocalizer>();
        var mockLogger = new Mock<ILogger<LanguageSelectionHandler>>();

        mockLocalizer
            .Setup(l => l.Get(200L, "language_saved"))
            .Returns("Language saved");

        var handler = new LanguageSelectionHandler(
            mockBotClient.Object,
            mockPrefRepo.Object,
            mockHistRepo.Object,
            mockLocalizer.Object,
            mockLogger.Object);

        var callbackQuery = LanguageCallbackQuery("ru");

        await handler.HandleAsync(callbackQuery, CancellationToken.None);

        mockHistRepo.Verify(
            r => r.RecordAsync(
                200L,
                100L,
                "Test",
                BotActionType.LanguageChanged,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Extracts_Language_Code_From_Callback()
    {
        var mockBotClient = new Mock<ITelegramBotClient>();
        var mockPrefRepo = new Mock<IPreferenceRepository>();
        var mockHistRepo = new Mock<IHistoryRepository>();
        var mockLocalizer = new Mock<ILocalizer>();
        var mockLogger = new Mock<ILogger<LanguageSelectionHandler>>();

        mockLocalizer
            .Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns("Preference saved");

        var handler = new LanguageSelectionHandler(
            mockBotClient.Object,
            mockPrefRepo.Object,
            mockHistRepo.Object,
            mockLocalizer.Object,
            mockLogger.Object);

        var callbackQuery = LanguageCallbackQuery("ru");

        await handler.HandleAsync(callbackQuery, CancellationToken.None);

        // Verify language was saved with correct code
        mockPrefRepo.Verify(
            r => r.SaveLanguageAsync(200L, "ru", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
