using Microsoft.Data.Sqlite;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class LoginCodeStoreTests : IDisposable
{
    private readonly SqliteConnection keepAlive;
    private readonly string connStr;

    public LoginCodeStoreTests()
    {
        var db = $"LoginCodeStoreTests_{Guid.NewGuid():N}";
        this.connStr = $"Data Source=file:{db}?mode=memory&cache=shared";
        this.keepAlive = new SqliteConnection(this.connStr);
        this.keepAlive.Open();
    }

    public void Dispose() => this.keepAlive.Dispose();

    private LoginCodeStore MakeStore(TimeProvider? time = null) =>
        new LoginCodeStore(this.connStr, time ?? TimeProvider.System);

    [Fact]
    public void GenerateCode_Returns6DigitCodeAndNonEmptyToken()
    {
        var store = MakeStore();
        var code = store.GenerateCode(out var token);

        Assert.Matches(@"^\d{6}$", code);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void TryVerify_ValidCode_ReturnsTrueAndSessionAvailable()
    {
        var store = MakeStore();
        var code = store.GenerateCode(out var token);

        var result = store.TryVerify(code, chatId: 42L);

        Assert.True(result);
        Assert.True(store.TryGetSession(token, out var chatId));
        Assert.Equal(42L, chatId);
    }

    [Fact]
    public void TryVerify_UnknownCode_ReturnsFalse()
    {
        var store = MakeStore();
        Assert.False(store.TryVerify("000000", chatId: 1L));
    }

    [Fact]
    public void TryVerify_WrongChatId_StillReturnsTrue_AndSessionHasCorrectChatId()
    {
        // The code itself doesn't restrict which chatId can verify — TryVerify accepts any chatId.
        // This test documents the actual contract: verification succeeds and the session carries
        // whatever chatId was supplied.
        var store = MakeStore();
        var code = store.GenerateCode(out var token);

        Assert.True(store.TryVerify(code, chatId: 99L));
        Assert.True(store.TryGetSession(token, out var chatId));
        Assert.Equal(99L, chatId);
    }

    [Fact]
    public void TryGetSession_UnknownToken_ReturnsFalse()
    {
        var store = MakeStore();
        Assert.False(store.TryGetSession("deadbeef", out var chatId));
        Assert.Equal(0L, chatId);
    }

    [Fact]
    public void TryGetSession_ExpiredToken_ReturnsFalse()
    {
        var now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);
        var store = MakeStore(fakeTime);

        var code = store.GenerateCode(out var token);
        store.TryVerify(code, chatId: 1L);

        // Advance past the 24 h token TTL
        fakeTime.Advance(TimeSpan.FromHours(25));

        Assert.False(store.TryGetSession(token, out var chatId));
        Assert.Equal(0L, chatId);
    }

    [Fact]
    public void Purge_RemovesCode_TryVerifyReturnsFalse()
    {
        var store = MakeStore();
        var code = store.GenerateCode(out _);
        store.Purge(code);
        Assert.False(store.TryVerify(code, chatId: 1L));
    }

    private sealed class FakeTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset current = initial;

        public void Advance(TimeSpan by) => this.current += by;

        public override DateTimeOffset GetUtcNow() => this.current;
    }
}
