using Moq;
using Telegram.Bot.Requests;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class BuyCommandHandlerIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task BulkBuy_ThreeItemsNoQuantity_CreatesThreeItems()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Молоко, Яйца, Хлеб"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Equal(3, items.Count);
        Assert.Contains(items, i => i.Name == "Молоко" && i.Quantity == null);
        Assert.Contains(items, i => i.Name == "Яйца" && i.Quantity == null);
        Assert.Contains(items, i => i.Name == "Хлеб" && i.Quantity == null);
    }

    [Fact]
    public async Task BulkBuy_TwoItemsWithQuantities_AddsCorrectQuantities()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Молоко 2л, Яйца 6"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Name == "Молоко" && i.Quantity != null);
        Assert.Contains(items, i => i.Name == "Яйца" && i.Quantity == "6");
    }

    [Fact]
    public async Task BulkBuy_WhitespacePadding_TrimsAndCreatesItems()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Молоко  ,  Яйца"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.Name)));
    }

    [Fact]
    public async Task BulkBuy_MoreThan20Items_OnlyFirst20Added()
    {
        await ClearDataAsync();

        var csv = string.Join(", ", Enumerable.Range(1, 21).Select(i => $"Item{i}"));
        await DispatchAsync(CommandUpdate(-100, 42, $"/buy {csv}"));

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Equal(20, items.Count);
    }

    [Fact]
    public async Task BulkBuy_RecordsHistoryForEachItem()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Молоко, Яйца, Хлеб"));

        var history = await HistoryRepository.GetRecentAsync(-100, 10, CancellationToken.None);
        Assert.Equal(3, history.Count(h => h.ActionType == ProductTrackerBot.Models.BotActionType.ItemAdded));
    }

    [Fact]
    public async Task BulkBuy_SendsConfirmationWithBulkAddedKey()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Молоко, Яйца"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text.Contains("shop.bulk-added")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SingleItemBuy_StillShowsReview_Backward_Compat()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Milk 2l"));

        // Item not immediately added — review step
        var group = await GroupRepository.GetOrCreateAsync(-100);
        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Empty(items);

        // Review message with confirm button was sent
        var confirmData = GetLastBuyConfirmCallbackData();
        Assert.NotNull(confirmData);
    }

    [Fact]
    public async Task DialogBuy_NoArgs_StillOpensTwoStepDialog()
    {
        await ClearDataAsync();

        await DispatchAsync(CommandUpdate(-100, 42, "/buy"));

        BotMock.Verify(
            b => b.SendRequest(
                It.Is<SendMessageRequest>(r => r.Text == "buy.what-to-buy"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
