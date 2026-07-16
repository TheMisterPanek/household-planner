# Tasks: Smart Count Parser for /buy Input

## T1 — Update `NormalizeUnit` in `BuyInputParser`

**File:** `ProductTrackerBot/Services/BuyInputParser.cs`

In the `NormalizeUnit` method, change the Russian piece-count branch so all variants (`шт`, `шт.`, `шту`, `шту.`, `штуку`, `штук`) return `"штук"` instead of `"шт"`:

```csharp
// Before
if (lower == "шт" || lower == "шт." || lower.StartsWith("штук") || lower.StartsWith("шту"))
{
    return "шт";
}

// After
if (lower == "шт" || lower == "шт." || lower.StartsWith("штук") || lower.StartsWith("шту"))
{
    return "штук";
}
```

No other changes to the method or regex.

---

## T2 — Update existing tests that expect `"шт"` output

**File:** `ProductTrackerBot.Tests/Services/BuyInputParserTests.cs`

Two tests currently assert `"1 шт"` for the abbreviated output. Update their `Assert.Equal` expectations to `"1 штук"`:

- `Parse_WithKupitPrefixAndUnit_ReturnsNormalizedQty` — change expected qty from `"1 шт"` → `"1 штук"`
- `Parse_WithoutKupitPrefix_ReturnsNormalizedQty` — same change

---

## T3 — Add new test cases for partial unit words

**File:** `ProductTrackerBot.Tests/Services/BuyInputParserTests.cs`

Add the following test cases to cover the change:

```csharp
[Fact]
public void Parse_ShtuSuffix_ReturnsShtuokCanonical()
{
    var (name, qty) = BuyInputParser.Parse("туалетка 8 шту");
    Assert.Equal("туалетка", name);
    Assert.Equal("8 штук", qty);
}

[Fact]
public void Parse_ShtukaUnit_ReturnsShtuokCanonical()
{
    var (name, qty) = BuyInputParser.Parse("яйца 12 штука");
    Assert.Equal("яйца", name);
    Assert.Equal("12 штук", qty);
}

[Fact]
public void Parse_ShtukoUnit_ReturnsShtuokCanonical()
{
    var (name, qty) = BuyInputParser.Parse("сырок 5 штуку");
    Assert.Equal("сырок", name);
    Assert.Equal("5 штук", qty);
}

[Fact]
public void Parse_FullShtuokUnit_ReturnsShtuokCanonical()
{
    var (name, qty) = BuyInputParser.Parse("вода 6 штук");
    Assert.Equal("вода", name);
    Assert.Equal("6 штук", qty);
}
```

---

## T4 — Run tests and verify build

```bash
make test
dotnet build
```

All tests must pass. Build must produce 0 errors.

---

## T5 — Smoke test (manual, do not mark complete until confirmed)

Send the following messages in a Telegram chat with the bot:

1. `/buy туалетка 8 шту` → review should read: **Add: туалетка ×8 штук?** (confirm → item added)
2. `/buy молоко 2 л` → review should still read: **Add: молоко ×2 л?** (non-count unit unchanged)
3. `/buy хлеб 3 штуку` → review should read: **Add: хлеб ×3 штук?**
4. `/buy eggs 12 pcs` → review should read: **Add: eggs ×12 pcs?** (Latin count unchanged)
5. `/buy купить отривин 1 штуку` → review should read: **Add: отривин ×1 штук?**

**Stop here.** Do not archive until the user confirms all 5 cases pass.
