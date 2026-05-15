using Moq;
using ProductTrackerBot.Models;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class SearchPricesIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task Search_With_Results_Sends_Formatted_Reply()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        await PurchaseRepository.AddAsync(new PurchaseRecord
        {
            GroupId = group.Id,
            UserId = 42,
            ItemName = "Milk",
            Quantity = "1l",
            StoreName = "Lidl",
            Price = 1.20m,
            PurchasedAt = DateTime.UtcNow,
            BoughtByName = "TestUser",
        });

        await DispatchAsync(CommandUpdate(-100, 42, "/search Milk"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text.Contains("Milk")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Search_No_Results_Sends_Not_Found_Reply()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/search UnknownItem"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Prices_With_Logged_Prices_Sends_Stats()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        await PriceLogRepository.AddAsync(group.Id, "Butter", 2.50m, "Aldi", DateTime.UtcNow);
        await PriceLogRepository.AddAsync(group.Id, "Butter", 3.00m, "Aldi", DateTime.UtcNow);

        await DispatchAsync(CommandUpdate(-100, 42, "/prices Butter"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text.Contains("Butter")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Prices_No_Data_Sends_Not_Found_Reply()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/prices NoSuchItem"));

        BotMock.Verify(
            b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
