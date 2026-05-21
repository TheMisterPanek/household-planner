using Moq;
using ProductTrackerBot.Models;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class AiCommandIntegrationTests : TelegramIntegrationTestBase
{
    // Task 8.2-8.4: group with LanguageCode="en" → system prompt contains "English", not "{primaryLanguage}"
    [Fact]
    public async Task AiCommand_GroupLanguageEn_AnswerAsyncCalledWithEnglish()
    {
        await ClearDataAsync();

        // Seed a group with LanguageCode = "en"
        var group = await GroupRepository.GetOrCreateAsync(-100);
        // GroupRepository defaults to "ru"; update it to "en" via LanguageRepository equivalent
        await using var conn = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Groups SET LanguageCode = 'en' WHERE ChatId = -100";
        await cmd.ExecuteNonQueryAsync();

        AiQueryServiceMock
            .Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("Answer in English", []));

        await DispatchAsync(CommandUpdate(-100, 42, "/ai what's on the list?"));

        AiQueryServiceMock.Verify(s => s.AnswerAsync(
            It.IsAny<long>(),
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            "English",
            It.IsAny<CancellationToken>()), Times.Once);

        BotMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => r.Text == "Answer in English"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Task 8.5: group with LanguageCode="ru" → AnswerAsync called with "Russian"
    [Fact]
    public async Task AiCommand_GroupLanguageRu_AnswerAsyncCalledWithRussian()
    {
        await ClearDataAsync();

        await GroupRepository.GetOrCreateAsync(-100);
        await using var conn = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Groups SET LanguageCode = 'ru' WHERE ChatId = -100";
        await cmd.ExecuteNonQueryAsync();

        AiQueryServiceMock
            .Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("Ответ по-русски", []));

        await DispatchAsync(CommandUpdate(-100, 42, "/ai what's on the list?"));

        AiQueryServiceMock.Verify(s => s.AnswerAsync(
            It.IsAny<long>(),
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            "Russian",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
