using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class ConversationHistoryServiceTests
{
    [Fact]
    public void GetRecentContext_NothingRecorded_ReturnsEmpty()
    {
        var svc = new ConversationHistoryService();
        Assert.Equal(string.Empty, svc.GetRecentContext(1));
    }

    [Fact]
    public void GetRecentContext_SingleEntry_ReturnsIt()
    {
        var svc = new ConversationHistoryService();
        svc.Record(1, "User: hello");
        Assert.Equal("User: hello", svc.GetRecentContext(1));
    }

    [Fact]
    public void GetRecentContext_MultipleEntries_JoinedByNewline()
    {
        var svc = new ConversationHistoryService();
        svc.Record(1, "User: hello");
        svc.Record(1, "Bot: hi there");
        Assert.Equal("User: hello\nBot: hi there", svc.GetRecentContext(1));
    }

    [Fact]
    public void GetRecentContext_DifferentChats_Isolated()
    {
        var svc = new ConversationHistoryService();
        svc.Record(1, "User: chat one");
        svc.Record(2, "User: chat two");
        Assert.Equal("User: chat one", svc.GetRecentContext(1));
        Assert.Equal("User: chat two", svc.GetRecentContext(2));
    }

    [Fact]
    public void GetRecentContext_LongHistory_TruncatedToLast500Chars()
    {
        var svc = new ConversationHistoryService();
        var longText = new string('A', 600);
        svc.Record(1, longText);
        var result = svc.GetRecentContext(1);
        Assert.Equal(500, result.Length);
        Assert.All(result.ToCharArray(), c => Assert.Equal('A', c));
    }

    [Fact]
    public void GetRecentContext_Exactly500Chars_NotTruncated()
    {
        var svc = new ConversationHistoryService();
        var text = new string('B', 500);
        svc.Record(1, text);
        Assert.Equal(500, svc.GetRecentContext(1).Length);
    }
}
