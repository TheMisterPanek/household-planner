using ProductTrackerBot.Handlers;

namespace ProductTrackerBot.Tests.Handlers;

public class ExpiryInputParserTests
{
    [Fact]
    public void Parse_Days_ReturnsToday_PlusDays()
    {
        var result = ExpiryInputParser.Parse("14");
        Assert.NotNull(result);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddDays(14), result!.Value);
    }

    [Fact]
    public void Parse_2Weeks_ReturnsToday_Plus14Days()
    {
        var result = ExpiryInputParser.Parse("2 weeks");
        Assert.NotNull(result);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddDays(14), result!.Value);
    }

    [Fact]
    public void Parse_1Month_ReturnsToday_Plus1Month()
    {
        var result = ExpiryInputParser.Parse("1 month");
        Assert.NotNull(result);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddMonths(1), result!.Value);
    }

    [Fact]
    public void Parse_Russian2Weeks_ReturnsToday_Plus14Days()
    {
        var result = ExpiryInputParser.Parse("2 недели");
        Assert.NotNull(result);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now).AddDays(14), result!.Value);
    }

    [Fact]
    public void Parse_Garbage_ReturnsNull()
    {
        var result = ExpiryInputParser.Parse("abc");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_Zero_ReturnsNull()
    {
        var result = ExpiryInputParser.Parse("0");
        Assert.Null(result);
    }

    [Fact]
    public void Parse_Over365_ReturnsNull()
    {
        var result = ExpiryInputParser.Parse("400");
        Assert.Null(result);
    }
}
