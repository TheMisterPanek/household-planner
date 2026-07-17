using Telegram.Bot.Types.ReplyMarkups;

namespace ProductTrackerBot.Tests.Integration;

[Collection("IntegrationTests")]
public class ListFilterIntegrationTests : TelegramIntegrationTestBase
{
    [Fact]
    public async Task List_NoCategorizedItems_ShowsNoFilterRow()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        await ItemRepository.AddAsync(group.Id, "Молоко", null, "TestUser");

        await DispatchAsync(CommandUpdate(-100, 42, "/list"));

        var sent = GetLastSentMessage();
        Assert.NotNull(sent);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(sent!.ReplyMarkup);
        Assert.DoesNotContain(
            keyboard.InlineKeyboard.SelectMany(row => row),
            btn => btn.CallbackData?.StartsWith("list_filter:") == true);
    }

    [Fact]
    public async Task List_WithCategorizedItems_FilterButtonScopesView_AndAllClearsIt()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        await ItemRepository.AddAsync(group.Id, "Порошок", null, "TestUser", null, "Химия");
        await ItemRepository.AddAsync(group.Id, "Молоко", null, "TestUser", null, "Еда");

        await DispatchAsync(CommandUpdate(-100, 42, "/list"));

        var sent = GetLastSentMessage();
        Assert.NotNull(sent);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(sent!.ReplyMarkup);
        var filterButton = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .First(btn => btn.CallbackData?.StartsWith("list_filter:") == true && btn.Text == "Химия");

        await DispatchAsync(CallbackUpdate(-100, 42, 1, filterButton.CallbackData!));

        var filteredEdit = GetLastEditedMessage();
        Assert.NotNull(filteredEdit);
        Assert.Contains("Порошок", filteredEdit!.Text);
        Assert.DoesNotContain("Молоко", filteredEdit.Text);

        var filteredKeyboard = Assert.IsType<InlineKeyboardMarkup>(filteredEdit.ReplyMarkup);
        var allButton = filteredKeyboard.InlineKeyboard
            .SelectMany(row => row)
            .First(btn => btn.CallbackData == "list_filter:-100:-1:1");

        await DispatchAsync(CallbackUpdate(-100, 42, 1, allButton.CallbackData!));

        var clearedEdit = GetLastEditedMessage();
        Assert.NotNull(clearedEdit);
        Assert.Contains("Порошок", clearedEdit!.Text);
        Assert.Contains("Молоко", clearedEdit.Text);
    }

    [Fact]
    public async Task FilteredView_PaginatesWithinCategory()
    {
        await ClearDataAsync();

        var group = await GroupRepository.GetOrCreateAsync(-100);
        for (int i = 1; i <= 12; i++)
        {
            await ItemRepository.AddAsync(group.Id, $"Товар{i}", null, "TestUser", null, "Химия");
        }

        await DispatchAsync(CommandUpdate(-100, 42, "/list"));

        var sent = GetLastSentMessage();
        Assert.NotNull(sent);
        var keyboard = Assert.IsType<InlineKeyboardMarkup>(sent!.ReplyMarkup);
        var filterButton = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .First(btn => btn.CallbackData?.StartsWith("list_filter:") == true && btn.Text == "Химия");

        await DispatchAsync(CallbackUpdate(-100, 42, 1, filterButton.CallbackData!));

        var page1Edit = GetLastEditedMessage();
        Assert.NotNull(page1Edit);
        var page1Keyboard = Assert.IsType<InlineKeyboardMarkup>(page1Edit!.ReplyMarkup);
        var nextButton = page1Keyboard.InlineKeyboard
            .SelectMany(row => row)
            .First(btn => btn.CallbackData?.StartsWith("list_filter:-100:0:2") == true);

        await DispatchAsync(CallbackUpdate(-100, 42, 1, nextButton.CallbackData!));

        var page2Edit = GetLastEditedMessage();
        Assert.NotNull(page2Edit);
        var page2Keyboard = Assert.IsType<InlineKeyboardMarkup>(page2Edit!.ReplyMarkup);
        var itemButtons = page2Keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Where(btn => btn.CallbackData?.StartsWith("shop:done:") == true)
            .Select(btn => btn.Text)
            .ToList();
        Assert.Equal(2, itemButtons.Count);
        Assert.Contains("✓ Товар11", itemButtons);
        Assert.Contains("✓ Товар12", itemButtons);
    }
}
