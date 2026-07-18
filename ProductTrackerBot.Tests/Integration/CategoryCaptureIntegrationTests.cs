using ProductTrackerBot.Models;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class CategoryCaptureIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task InlineSingleAdd_TapSuggestion_PersistsCategory()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        await PurchaseRepository.AddAsync(new PurchaseRecord
        {
            GroupId = group.Id,
            UserId = 42,
            ItemName = "Порошок",
            BoughtByName = "TestUser",
            PurchasedAt = DateTime.UtcNow,
            Category = "Химия",
        });

        // Inline single add sends a review step; confirm it to persist the item.
        await DispatchAsync(CommandUpdate(-100, 42, "/buy Молоко 2л"));
        var confirmData = GetLastBuyConfirmCallbackData();
        Assert.NotNull(confirmData);
        await DispatchAsync(CallbackUpdate(-100, 42, 1, confirmData));

        var suggestData = GetLastCategorySuggestCallbackData();
        Assert.NotNull(suggestData);
        await DispatchAsync(CallbackUpdate(-100, 42, 2, suggestData));

        var items = await ItemRepository.GetAllAsync(group.Id);
        var milk = Assert.Single(items, i => i.Name == "Молоко");
        Assert.Equal("Химия", milk.Category);
    }

    [Fact]
    public async Task InlineSingleAdd_FreeTextReply_CreatesNewCategory()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Молоко 2л"));
        var confirmData = GetLastBuyConfirmCallbackData();
        Assert.NotNull(confirmData);
        await DispatchAsync(CallbackUpdate(-100, 42, 1, confirmData));

        // No suggestions exist yet; reply with free text to create a brand-new category.
        await DispatchAsync(MessageUpdate(-100, 42, "Для стирки"));

        var items = await ItemRepository.GetAllAsync(group.Id);
        var milk = Assert.Single(items, i => i.Name == "Молоко");
        Assert.Equal("Для стирки", milk.Category);
    }

    [Fact]
    public async Task BulkAdd_SingleCategoryPrompt_AppliesToAllItems()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CommandUpdate(-100, 42, "/buy Молоко, Яйца, Хлеб"));

        // Exactly one category prompt for the whole batch.
        var suggestOrSkip = GetLastCategorySuggestCallbackData();
        Assert.Null(suggestOrSkip); // no purchase history yet, so only the skip button is shown

        await DispatchAsync(MessageUpdate(-100, 42, "Бакалея"));

        var items = await ItemRepository.GetAllAsync(group.Id);
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal("Бакалея", i.Category));
    }

    [Fact]
    public async Task DialogSkipQuantity_ThenSkipCategory_LeavesItemUncategorized()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);

        await DispatchAsync(CommandUpdate(-100, 42, "/buy"));
        await DispatchAsync(MessageUpdate(-100, 42, "Rice"));
        await DispatchAsync(CallbackUpdate(-100, 42, 99, "buy:skip_quantity"));

        await DispatchAsync(CallbackUpdate(-100, 42, 100, "category:skip"));

        var items = await ItemRepository.GetAllAsync(group.Id);
        var rice = Assert.Single(items, i => i.Name == "Rice");
        Assert.Null(rice.Category);
    }

    [Fact]
    public async Task ItemEdit_ReopensCategoryPrompt_ChangesCategory()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Bread", null, "TestUser", null, "Бакалея");

        await DispatchAsync(CallbackUpdate(-100, 42, 1, $"item:edit:{item.Id}"));
        await DispatchAsync(MessageUpdate(-100, 42, "Bread 2 loaves"));

        var saveData = GetLastItemSaveCallbackData();
        Assert.NotNull(saveData);
        await DispatchAsync(CallbackUpdate(-100, 42, 2, saveData));

        await DispatchAsync(MessageUpdate(-100, 42, "Свежая выпечка"));

        var updated = await ItemRepository.GetByIdAsync(item.Id);
        Assert.NotNull(updated);
        Assert.Equal("Свежая выпечка", updated!.Category);
    }

    [Fact]
    public async Task BoughtItem_WithCategory_CarriesCategoryIntoPurchaseHistory()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        var item = await ItemRepository.AddAsync(group.Id, "Bleach", null, "TestUser", null, "Химия");
        Assert.Equal("Химия", item.Category);
        var reloaded = await ItemRepository.GetByIdAsync(item.Id);
        Assert.Equal("Химия", reloaded!.Category);

        await DispatchAsync(CallbackUpdate(-100, 42, 1, $"shop:done:{item.Id}"));
        await DispatchAsync(CallbackUpdate(-100, 42, 2, "price:skip_store"));
        await DispatchAsync(CallbackUpdate(-100, 42, 2, "price:skip_price"));
        await DispatchAsync(CallbackUpdate(-100, 42, 2, "price:skip_expiry"));

        var records = await PurchaseRepository.SearchAsync(group.Id, "Bleach");
        Assert.Contains(records, r => r.Category == "Химия");
    }

    [Fact]
    public async Task CategorySuggestions_SharedAcrossUsersInSameGroup()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        await PurchaseRepository.AddAsync(new PurchaseRecord
        {
            GroupId = group.Id,
            UserId = 1,
            ItemName = "Стиральный порошок",
            BoughtByName = "Alice",
            PurchasedAt = DateTime.UtcNow,
            Category = "Химия",
        });

        // A different user (99) in the same group triggers a new category prompt.
        // No quantity is detected, so the item is saved immediately (no review step).
        await DispatchAsync(CommandUpdate(-100, 99, "/buy Отбеливатель"));

        var suggestData = GetLastCategorySuggestCallbackData();
        Assert.NotNull(suggestData);
        await DispatchAsync(CallbackUpdate(-100, 99, 2, suggestData));

        var items = await ItemRepository.GetAllAsync(group.Id);
        var item = Assert.Single(items, i => i.Name == "Отбеливатель");
        Assert.Equal("Химия", item.Category);
    }
}
