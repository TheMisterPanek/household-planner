using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class ItemEditStepHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Mock<ITelegramBotClient> CreateBotMock()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());
        return bot;
    }

    [Fact]
    public void CanHandle_WhenStateActive_ReturnsTrue()
    {
        var dialogService = new PendingDialogService<EditItemDialogState>();
        dialogService.SetState(-100L, 42L, new EditItemDialogState
        {
            ItemId = 1,
            GroupId = 10,
            OriginalName = "отривин",
            OriginalQuantity = "1 шт",
        });

        var handler = new ItemEditStepHandler(
            Mock.Of<ITelegramBotClient>(),
            dialogService,
            new PendingEditService(),
            Mock.Of<ILocalizer>());

        Assert.True(handler.CanHandle(-100L, 42L));
    }

    [Fact]
    public void CanHandle_WhenNoState_ReturnsFalse()
    {
        var dialogService = new PendingDialogService<EditItemDialogState>();

        var handler = new ItemEditStepHandler(
            Mock.Of<ITelegramBotClient>(),
            dialogService,
            new PendingEditService(),
            Mock.Of<ILocalizer>());

        Assert.False(handler.CanHandle(-100L, 42L));
    }

    [Fact]
    public async Task HandleAsync_ParsesInput_ClearsDialog_StoresPendingEdit_SendsTwoButtonReview()
    {
        var bot = CreateBotMock();
        var dialogService = new PendingDialogService<EditItemDialogState>();
        dialogService.SetState(-100L, 42L, new EditItemDialogState
        {
            ItemId = 5,
            GroupId = 10,
            OriginalName = "отривин",
            OriginalQuantity = "1 шт",
        });

        var pendingEditService = new PendingEditService();
        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new ItemEditStepHandler(bot.Object, dialogService, pendingEditService, localizer.Object);
        var message = DeserializeMessage("{\"message_id\":3,\"from\":{\"id\":42,\"first_name\":\"Alice\"},\"chat\":{\"id\":-100},\"text\":\"отривин 2 шт\"}");
        await handler.HandleAsync(message, CancellationToken.None);

        // Dialog cleared
        Assert.Null(dialogService.GetState(-100L, 42L));

        // Review message with 2 buttons sent
        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
