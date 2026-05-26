using Moq;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class ExpiryDaySuggestionServiceTests
{
    private static (ExpiryDaySuggestionService Service, Mock<PurchaseHistoryRepository> Repo) Create()
    {
        var repo = new Mock<PurchaseHistoryRepository>("Data Source=file::memory:");
        var service = new ExpiryDaySuggestionService(repo.Object);
        return (service, repo);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ReturnsUpTo3_WhenSimilarItemsFound()
    {
        var (service, repo) = Create();
        repo.Setup(r => r.GetExpiryDaySuggestionsAsync(1, "milk"))
            .ReturnsAsync(new[] { 7, 14, 30, 3 }.AsReadOnly());

        var result = await service.GetSuggestionsAsync(1, "milk");

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 7, 14, 30 }, result);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ReturnsFallbackAverage_WhenNoSimilarItemsButGroupHasHistory()
    {
        var (service, repo) = Create();
        repo.Setup(r => r.GetExpiryDaySuggestionsAsync(1, "newitem"))
            .ReturnsAsync(Array.Empty<int>().AsReadOnly());
        repo.Setup(r => r.GetAverageExpiryDaysAsync(1))
            .ReturnsAsync(14);

        var result = await service.GetSuggestionsAsync(1, "newitem");

        Assert.Single(result);
        Assert.Equal(14, result[0]);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ReturnsEmpty_WhenGroupHasNoExpiryHistory()
    {
        var (service, repo) = Create();
        repo.Setup(r => r.GetExpiryDaySuggestionsAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<int>().AsReadOnly());
        repo.Setup(r => r.GetAverageExpiryDaysAsync(It.IsAny<int>()))
            .ReturnsAsync(0);

        var result = await service.GetSuggestionsAsync(1, "anything");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSuggestionsAsync_UsesFirstWordOfItemNameAsKeyword()
    {
        var (service, repo) = Create();
        repo.Setup(r => r.GetExpiryDaySuggestionsAsync(1, "whole"))
            .ReturnsAsync(new[] { 7 }.AsReadOnly());

        var result = await service.GetSuggestionsAsync(1, "Whole Milk 1L");

        Assert.Single(result);
        Assert.Equal(7, result[0]);
        repo.Verify(r => r.GetExpiryDaySuggestionsAsync(1, "whole"), Times.Once);
    }

    [Fact]
    public async Task GetSuggestionsAsync_DoesNotCallAverage_WhenSimilarItemsFound()
    {
        var (service, repo) = Create();
        repo.Setup(r => r.GetExpiryDaySuggestionsAsync(1, "milk"))
            .ReturnsAsync(new[] { 7 }.AsReadOnly());

        await service.GetSuggestionsAsync(1, "milk");

        repo.Verify(r => r.GetAverageExpiryDaysAsync(It.IsAny<int>()), Times.Never);
    }
}
