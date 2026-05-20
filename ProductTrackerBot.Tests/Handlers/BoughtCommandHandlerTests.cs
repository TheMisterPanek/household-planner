using System.Text.Json;
using Moq;
using ProductTrackerBot.Handlers;
using ProductTrackerBot.Localization;
using ProductTrackerBot.Models;
using ProductTrackerBot.Repositories;
using ProductTrackerBot.Services;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace ProductTrackerBot.Tests.Handlers;

public class BoughtCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static Message DeserializeMessage(string json) =>
        JsonSerializer.Deserialize<Message>(json, JsonOpts)!;

    private static Message GroupMessage(string text) =>
        DeserializeMessage($"{{\"message_id\":1,\"from\":{{\"id\":42,\"first_name\":\"Alice\"}},\"chat\":{{\"id\":-100,\"type\":\"supergroup\"}},\"text\":\"{text}\"}}");

    private static Message PrivateMessage(string text) =>
        DeserializeMessage($"{{\"message_id\":1,\"from\":{{\"id\":42,\"first_name\":\"Alice\"}},\"chat\":{{\"id\":42,\"type\":\"private\"}},\"text\":\"{text}\"}}");

    private (BoughtCommandHandler Handler, Mock<ITelegramBotClient> Bot, PendingDialogService<BoughtDialogState> DialogService) CreateHandler()
    {
        var bot = new Mock<ITelegramBotClient>();
        bot.Setup(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message());

        var groupRepo = new Mock<GroupRepository>("Data Source=file::memory:");
        groupRepo.Setup(r => r.GetOrCreateAsync(-100L))
            .ReturnsAsync(new Group { Id = 10, ChatId = -100L });

        var dialogService = new PendingDialogService<BoughtDialogState>();

        var localizer = new Mock<ILocalizer>();
        localizer.Setup(l => l.Get(It.IsAny<long>(), It.IsAny<string>()))
            .Returns<long, string>((_, key) => key);

        var handler = new BoughtCommandHandler(bot.Object, groupRepo.Object, dialogService, localizer.Object);
        return (handler, bot, dialogService);
    }

    [Fact]
    public async Task PrivateChat_SendsGroupOnly_NoStateSet()
    {
        var (handler, bot, dialogService) = CreateHandler();

        await handler.HandleAsync(PrivateMessage("/bought"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(r =>
            r.Text == "common.group-only"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(dialogService.GetState(42L, 42L));
    }

    [Fact]
    public async Task NoArgs_SendsWhatDidYouBuy_Step1StateSet()
    {
        var (handler, bot, dialogService) = CreateHandler();

        await handler.HandleAsync(GroupMessage("/bought"), CancellationToken.None);

        bot.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(r =>
            r.Text == "bought.what-did-you-buy"), It.IsAny<CancellationToken>()), Times.Once);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(1, state!.Step);
    }

    [Fact]
    public async Task InlineArgsWithQuantity_SetsStep2_ItemNameAndQuantity()
    {
        var (handler, bot, dialogService) = CreateHandler();

        await handler.HandleAsync(GroupMessage("/bought milk 1L"), CancellationToken.None);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Equal("milk", state.ItemName);
        Assert.Equal("1L", state.Quantity);

        bot.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InlineArgsNoQuantity_SetsStep2_NullQuantity()
    {
        var (handler, bot, dialogService) = CreateHandler();

        await handler.HandleAsync(GroupMessage("/bought eggs"), CancellationToken.None);

        var state = dialogService.GetState(-100L, 42L);
        Assert.NotNull(state);
        Assert.Equal(2, state!.Step);
        Assert.Equal("eggs", state.ItemName);
        Assert.Null(state.Quantity);
    }
}
