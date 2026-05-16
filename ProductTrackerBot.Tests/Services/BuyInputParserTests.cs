using ProductTrackerBot.Services;

namespace ProductTrackerBot.Tests.Services;

public class BuyInputParserTests
{
    [Fact]
    public void Parse_WithKupitPrefixAndUnit_ReturnsNormalizedQty()
    {
        var (name, qty) = BuyInputParser.Parse("купить отривин 1 штуку");
        Assert.Equal("отривин", name);
        Assert.Equal("1 шт", qty);
    }

    [Fact]
    public void Parse_LitreUnit_ReturnsNameAndQty()
    {
        var (name, qty) = BuyInputParser.Parse("молоко 2 л");
        Assert.Equal("молоко", name);
        Assert.Equal("2 л", qty);
    }

    [Fact]
    public void Parse_NoQty_ReturnsNullQty()
    {
        var (name, qty) = BuyInputParser.Parse("хлеб");
        Assert.Equal("хлеб", name);
        Assert.Null(qty);
    }

    [Fact]
    public void Parse_BareNumber_ReturnsNumberAsQty()
    {
        var (name, qty) = BuyInputParser.Parse("хлеб 2");
        Assert.Equal("хлеб", name);
        Assert.Equal("2", qty);
    }

    [Fact]
    public void Parse_NoTrailingNumber_ReturnsFullTextAsName()
    {
        var (name, qty) = BuyInputParser.Parse("зубная паста colgate max fresh");
        Assert.Equal("зубная паста colgate max fresh", name);
        Assert.Null(qty);
    }

    [Fact]
    public void Parse_WithoutKupitPrefix_ReturnsNormalizedQty()
    {
        var (name, qty) = BuyInputParser.Parse("отривин 1 штуку");
        Assert.Equal("отривин", name);
        Assert.Equal("1 шт", qty);
    }

    [Fact]
    public void Parse_DecimalQuantity_ReturnsDecimalQty()
    {
        var (name, qty) = BuyInputParser.Parse("молоко 0.5 л");
        Assert.Equal("молоко", name);
        Assert.Equal("0.5 л", qty);
    }

    [Fact]
    public void Parse_LatinUnitPcs_ReturnsQtyWithPcs()
    {
        var (name, qty) = BuyInputParser.Parse("eggs 12 pcs");
        Assert.Equal("eggs", name);
        Assert.Equal("12 pcs", qty);
    }
}
