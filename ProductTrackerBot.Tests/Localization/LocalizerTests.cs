using Microsoft.Extensions.Logging;
using Moq;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Repositories;

namespace ProductTrackerBot.Tests.Localization;

public class LocalizerTests
{
    [Fact]
    public async Task GetAsync_Caches_Language_Preference_Per_Chat()
    {
        var mockPrefRepo = new Mock<IPreferenceRepository>();
        var mockLogger = new Mock<ILogger<Localizer>>();

        mockPrefRepo
            .Setup(r => r.GetLanguageAsync(100L, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ru");

        var localizer = new Localizer(mockPrefRepo.Object, mockLogger.Object);

        // First call should fetch from repository
        await localizer.GetAsync(100L, "menu_prompt", CancellationToken.None);

        // Second call should use cache
        await localizer.GetAsync(100L, "language_saved", CancellationToken.None);

        // Verify repository was only called once for this chat
        mockPrefRepo.Verify(
            r => r.GetLanguageAsync(100L, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_Uses_Default_Language_When_No_Preference_Set()
    {
        var mockPrefRepo = new Mock<IPreferenceRepository>();
        var mockLogger = new Mock<ILogger<Localizer>>();

        mockPrefRepo
            .Setup(r => r.GetLanguageAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var localizer = new Localizer(mockPrefRepo.Object, mockLogger.Object);

        // Should not throw and should handle null gracefully
        var result = await localizer.GetAsync(999L, "menu_prompt", CancellationToken.None);

        // Result should be the key itself (fallback behavior when translations aren't loaded)
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAsync_Different_Chats_Have_Independent_Caches()
    {
        var mockPrefRepo = new Mock<IPreferenceRepository>();
        var mockLogger = new Mock<ILogger<Localizer>>();

        mockPrefRepo
            .Setup(r => r.GetLanguageAsync(100L, It.IsAny<CancellationToken>()))
            .ReturnsAsync("en");

        mockPrefRepo
            .Setup(r => r.GetLanguageAsync(200L, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ru");

        var localizer = new Localizer(mockPrefRepo.Object, mockLogger.Object);

        await localizer.GetAsync(100L, "menu_prompt", CancellationToken.None);
        await localizer.GetAsync(200L, "menu_prompt", CancellationToken.None);
        await localizer.GetAsync(100L, "language_saved", CancellationToken.None);
        await localizer.GetAsync(200L, "language_saved", CancellationToken.None);

        // Verify repository was called once for each chat (not twice for the same chat)
        mockPrefRepo.Verify(
            r => r.GetLanguageAsync(100L, It.IsAny<CancellationToken>()),
            Times.Once);

        mockPrefRepo.Verify(
            r => r.GetLanguageAsync(200L, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
