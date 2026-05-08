using ProductTrackerBot.Localization;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class PendingDialogServiceTests
{
    [Fact]
    public void SetState_And_GetState_Should_Return_Same_State()
    {
        var service = new PendingDialogService<TestState>();
        var state = new TestState { Value = "hello" };

        service.SetState(chatId: 1, userId: 100, state);
        var retrieved = service.GetState(1, 100);

        Assert.NotNull(retrieved);
        Assert.Equal("hello", retrieved!.Value);
    }

    [Fact]
    public void GetState_With_No_State_Should_Return_Null()
    {
        var service = new PendingDialogService<TestState>();

        var state = service.GetState(1, 100);

        Assert.Null(state);
    }

    [Fact]
    public void ClearState_Should_Remove_State()
    {
        var service = new PendingDialogService<TestState>();
        service.SetState(chatId: 1, userId: 100, new TestState());

        var removed = service.ClearState(1, 100);

        Assert.True(removed);
        Assert.Null(service.GetState(1, 100));
    }

    [Fact]
    public void ClearState_With_No_State_Should_Return_False()
    {
        var service = new PendingDialogService<TestState>();

        var removed = service.ClearState(1, 100);

        Assert.False(removed);
    }

    [Fact]
    public void Different_Users_Should_Have_Separate_States()
    {
        var service = new PendingDialogService<TestState>();
        var alice = new TestState { Value = "Alice" };
        var bob = new TestState { Value = "Bob" };

        service.SetState(chatId: 1, userId: 100, alice);
        service.SetState(chatId: 1, userId: 200, bob);

        Assert.Equal("Alice", service.GetState(1, 100)!.Value);
        Assert.Equal("Bob", service.GetState(1, 200)!.Value);
    }

    [Fact]
    public void Different_Chats_Should_Have_Separate_States()
    {
        var service = new PendingDialogService<TestState>();
        var chat1 = new TestState { Value = "Chat1" };
        var chat2 = new TestState { Value = "Chat2" };

        service.SetState(chatId: 1, userId: 100, chat1);
        service.SetState(chatId: 2, userId: 100, chat2);

        Assert.Equal("Chat1", service.GetState(1, 100)!.Value);
        Assert.Equal("Chat2", service.GetState(2, 100)!.Value);
    }

    [Fact]
    public void Concurrent_Access_Should_Work_Correctly()
    {
        var service = new PendingDialogService<TestState>();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var userId = i;
            tasks.Add(Task.Run(() =>
            {
                service.SetState(chatId: 1, userId: userId, new TestState { Value = userId.ToString() });
                var state = service.GetState(1, userId);
                Assert.NotNull(state);
                Assert.Equal(userId.ToString(), state!.Value);
                service.ClearState(1, userId);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Verify all states are cleared
        for (int i = 0; i < 100; i++)
        {
            Assert.Null(service.GetState(1, i));
        }
    }

    private class TestState
    {
        public string? Value { get; set; }
    }
}