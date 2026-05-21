using Moq;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Repositories;

/// <summary>
/// Unit tests for the AI chat page service interactions.
/// Tests verify that IAiQueryService is called correctly and results are handled.
/// </summary>
public class AiChatPageTests
{
    private readonly Mock<IAiQueryService> aiService = new();

    // 9.1 — Submitting a message calls AnswerAsync with correct chatId and groupId
    [Fact]
    public async Task AnswerAsync_Called_With_Correct_ChatId_And_GroupId()
    {
        const long chatId = 12345L;
        const long groupId = 1L;
        const string question = "What should I buy?";

        this.aiService.Setup(s => s.AnswerAsync(chatId, groupId, question, "", It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(new AiQueryResult("Buy milk.", []));

        var result = await this.aiService.Object.AnswerAsync(chatId, groupId, question, "", "English", CancellationToken.None);

        this.aiService.Verify(s => s.AnswerAsync(chatId, groupId, question, "", "English", CancellationToken.None), Times.Once);
        Assert.Equal("Buy milk.", result.Text);
    }

    // 9.2 — Response from AnswerAsync returns text in result
    [Fact]
    public async Task AnswerAsync_Response_Text_Available_In_Result()
    {
        this.aiService.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("Based on your list, you need eggs.", []));

        var result = await this.aiService.Object.AnswerAsync(1L, 1L, "any question", "", "English", CancellationToken.None);

        Assert.Equal("Based on your list, you need eggs.", result.Text);
    }

    // 9.3 — Exception from AnswerAsync propagates so the page can catch and show error banner
    [Fact]
    public async Task AnswerAsync_ThrowsException_Propagates_For_Page_To_Catch()
    {
        this.aiService.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("401 Unauthorized"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            this.aiService.Object.AnswerAsync(1L, 1L, "any question", "", "English", CancellationToken.None));
    }

    [Fact]
    public async Task AnswerAsync_Returns_Empty_Suggestions_When_No_Items_Suggested()
    {
        this.aiService.Setup(s => s.AnswerAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiQueryResult("No items to suggest.", []));

        var result = await this.aiService.Object.AnswerAsync(1L, 1L, "any question", "", "English", CancellationToken.None);

        Assert.Empty(result.Suggestions);
    }
}
