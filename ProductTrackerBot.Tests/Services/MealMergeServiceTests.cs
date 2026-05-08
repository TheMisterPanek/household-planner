using ProductTrackerBot.Models;
using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class MealMergeServiceTests
{
    [Fact]
    public void CreateSession_Returns_8Char_Alphanumeric_Key()
    {
        var service = new MealMergeService();
        var conflicts = new List<MergeConflict>();

        var sessionId = service.CreateSession(chatId: 123, mealId: 1, conflicts);

        Assert.Equal(8, sessionId.Length);
        Assert.All(sessionId, c => Assert.True(char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void CreateSession_Makes_Session_Retrievable()
    {
        var service = new MealMergeService();
        var conflicts = new List<MergeConflict> { new MergeConflict { IngredientName = "Milk" } };

        var sessionId = service.CreateSession(chatId: 123, mealId: 1, conflicts);
        var session = service.TryGetSession(sessionId);

        Assert.NotNull(session);
        Assert.Equal(123, session.ChatId);
        Assert.Equal(1, session.MealId);
        Assert.Single(session.Conflicts);
    }

    [Fact]
    public void TryGetSession_Returns_Null_For_Unknown_Key()
    {
        var service = new MealMergeService();

        var session = service.TryGetSession("unknown");

        Assert.Null(session);
    }

    [Fact]
    public void TryGetSession_Purges_Old_Sessions()
    {
        var service = new MealMergeService();
        var conflicts = new List<MergeConflict>();

        var sessionId = service.CreateSession(chatId: 123, mealId: 1, conflicts);
        
        // Manipulate the session's creation time to be > 15 minutes old
        var session = service.TryGetSession(sessionId);
        Assert.NotNull(session);
        session.CreatedAt = DateTime.UtcNow.AddMinutes(-20);

        // Next TryGetSession should purge it
        var retrievedAfter = service.TryGetSession(sessionId);

        Assert.Null(retrievedAfter);
    }

    [Fact]
    public void ResolveConflict_Marks_Index_As_Resolved()
    {
        var service = new MealMergeService();
        var conflicts = new List<MergeConflict>
        {
            new MergeConflict { IngredientName = "Milk" },
            new MergeConflict { IngredientName = "Bread" },
        };

        var sessionId = service.CreateSession(chatId: 123, mealId: 1, conflicts);
        service.ResolveConflict(sessionId, index: 0, add: true);
        service.ResolveConflict(sessionId, index: 1, add: false);

        var session = service.TryGetSession(sessionId);
        Assert.True(session!.Resolved[0]);
        Assert.False(session.Resolved[1]);
    }
}
