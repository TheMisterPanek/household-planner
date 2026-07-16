# Design: Smart Expiry Day Suggestions

## Overview

This change adds smart suggestion buttons to every expiry-date prompt. When the bot asks for a shelf-life in days, it derives suggestions from past `PurchaseHistory` records (same item, or group-wide average) and renders them as inline buttons. Users tap a suggestion to finish the dialog without typing.

Two flows are affected:
- **`/bought` dialog** — `BoughtStepHandler` step 2 (after item name is entered)
- **Price-capture dialog** — `PriceCaptureStepHandler` step 3 (after store + price)

---

## Data Flow

```
[BoughtStepHandler.HandleStep1Async  OR  PriceCaptureStepHandler.HandleStep2Async]
  │
  ├─ ExpiryDaySuggestionService.GetSuggestionsAsync(groupId, itemName)
  │     ├─ PurchaseHistoryRepository.GetExpiryDaySuggestionsAsync(groupId, itemName)
  │     │     → SELECT ROUND(julianday(exp_date) - julianday(date(PurchasedAt))) AS days,
  │     │              COUNT(*) AS freq
  │     │         FROM PurchaseHistory
  │     │        WHERE GroupId = @groupId
  │     │          AND exp_date IS NOT NULL
  │     │          AND LOWER(ItemName) LIKE '%' || LOWER(@keyword) || '%'
  │     │        GROUP BY days ORDER BY freq DESC LIMIT 5
  │     │
  │     ├─ If result has ≥ 1 row → take up to 3 distinct day values → return them
  │     │
  │     └─ Else: PurchaseHistoryRepository.GetAverageExpiryDaysAsync(groupId)
  │           → SELECT CAST(ROUND(AVG(julianday(exp_date) - julianday(date(PurchasedAt)))) AS INT)
  │               FROM PurchaseHistory
  │              WHERE GroupId = @groupId AND exp_date IS NOT NULL
  │           → If result > 0 → return [avg] (single suggestion)
  │           → Else → return []
  │
  └─ Build InlineKeyboardMarkup
        Suggestions row: [7 days] [14 days] [30 days]    ← from service (0–3 buttons)
        Skip row:        [Skip]                           ← always present
```

### Sequence Diagram — `/bought` flow

```
User                BoughtCommandHandler     BoughtStepHandler      ExpiryDaySuggestionService   PurchaseHistoryRepository
 |                         |                        |                          |                          |
 |--/bought milk---------->|                        |                          |                          |
 |                         |--SetState(step=2)----->|                          |                          |
 |                         |--"What did you buy?"-->|                          |                          |
 |--milk (step 1)--------->|                        |                          |                          |
 |                         |                        |--GetSuggestionsAsync(milk)>                        |
 |                         |                        |                          |--GetExpiryDaySuggestions->|
 |                         |                        |                          |<--[7,14]-----------------|
 |                         |                        |<--[7,14]-----------------|                          |
 |                         |                        |--SendMessage(prompt + [7 days][14 days][Skip])---->|
 |<--"📅 Expiry for milk?" + [7 days][14 days][Skip]|                         |                          |
 |                         |                        |                          |                          |
 |--[7 days] tap---------->ExpirySuggestCallbackHandler                        |                          |
 |                         |--GetState(BoughtDialog step 2)                    |                          |
 |                         |--FinishDialogAsync(state, DateOnly+7)             |                          |
 |<--"✓ milk recorded, expires 29.05.2026"          |                          |                          |
```

---

## New: `ExpiryDaySuggestionService`

```csharp
// Services/ExpiryDaySuggestionService.cs
public class ExpiryDaySuggestionService
{
    private readonly PurchaseHistoryRepository purchaseRepository;

    public ExpiryDaySuggestionService(PurchaseHistoryRepository purchaseRepository)
        => this.purchaseRepository = purchaseRepository;

    // Returns up to 3 distinct day-count suggestions for itemName in the group.
    // Falls back to group-wide average if no similar-item data exists.
    // Returns empty list when the group has no expiry history at all.
    public async Task<IReadOnlyList<int>> GetSuggestionsAsync(int groupId, string itemName)
    {
        var keyword = ExtractKeyword(itemName);  // first word, lower-cased
        var similar = await this.purchaseRepository.GetExpiryDaySuggestionsAsync(groupId, keyword);
        if (similar.Count > 0)
            return similar.Take(3).ToList().AsReadOnly();

        var avg = await this.purchaseRepository.GetAverageExpiryDaysAsync(groupId);
        return avg > 0 ? new[] { avg }.AsReadOnly() : Array.Empty<int>().AsReadOnly();
    }

    private static string ExtractKeyword(string itemName)
    {
        var first = itemName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? itemName;
        return first.ToLowerInvariant();
    }
}
```

**Registration**: `builder.Services.AddScoped<ExpiryDaySuggestionService>();`

---

## New Repository Methods

```csharp
// Returns distinct shelf-life day counts for items whose name matches keyword,
// ordered by frequency descending. Days are rounded to nearest integer.
public virtual async Task<IReadOnlyList<int>> GetExpiryDaySuggestionsAsync(int groupId, string keyword)
{
    // SQL (see data flow above)
    // Returns List<int> of distinct rounded day counts, up to 5 rows.
}

// Returns the group-wide average shelf-life in days (rounded), or 0 if no data.
public virtual async Task<int> GetAverageExpiryDaysAsync(int groupId)
{
    // SELECT CAST(ROUND(AVG(julianday(exp_date) - julianday(date(PurchasedAt)))) AS INT)
    // Returns 0 if null (no rows with both fields set).
}
```

---

## New: `ExpirySuggestCallbackHandler`

```
CallbackPrefix: "expiry"
Handles:        "expiry:suggest:{days}"
```

```csharp
public class ExpirySuggestCallbackHandler : ICallbackHandler
{
    public string CallbackPrefix => "expiry";

    // 1. Parse days from callback data.
    // 2. Compute expDate = DateOnly.FromDateTime(DateTime.Now.AddDays(days)).
    // 3. Check PendingDialogService<BoughtDialogState> for state at step 2.
    //    → If found: await boughtStepHandler.FinishDialogAsync(chatId, userId, state, expDate, ct)
    // 4. Else check PendingDialogService<PriceCaptureDialogState> for state at step 3.
    //    → If found: await priceCaptureStepHandler.FinishDialogAsync(chatId, userId, state, expDate, ct)
    // 5. Else: AnswerCallbackQuery silently (stale tap).
    // 6. Always AnswerCallbackQuery to dismiss the loading spinner.
}
```

**Registration**: `builder.Services.AddScoped<ICallbackHandler, ExpirySuggestCallbackHandler>();`

---

## Handler Changes

### `BoughtStepHandler.HandleStep1Async`

Before sending the expiry prompt, fetch suggestions:

```csharp
var suggestions = await this.suggestionService.GetSuggestionsAsync(state.GroupId, itemName);
var buttons = suggestions
    .Select(d => InlineKeyboardButton.WithCallbackData(
        this.localizer.Get(chatId, "expiry.suggest-days").Replace("{n}", d.ToString()),
        $"expiry:suggest:{d}"))
    .ToList();
buttons.Add(InlineKeyboardButton.WithCallbackData(
    this.localizer.Get(chatId, "bought.skip-expiry"),
    "bought:skip_expiry"));

var keyboard = new InlineKeyboardMarkup(new[] { buttons.ToArray() });
```

### `PriceCaptureStepHandler.HandleStep2Async`

Same pattern after price is recorded and before step 3 prompt is sent. Also make `FinishDialogAsync` `public` so `ExpirySuggestCallbackHandler` can delegate to it.

---

## Keyboard Layout

```
Step 2 prompt (BoughtStepHandler):
┌─────────────────────────────────────────┐
│ 📅 Expiry date for Milk? (e.g. 30, 7 days) │
│  [7 days]  [14 days]  [30 days]  [Skip] │
└─────────────────────────────────────────┘

When no similar-item data, one fallback button:
│  [14 days]  [Skip]                      │

When no group history at all:
│  [Skip]                                 │
```

---

## Callback Data Sizes

| Callback | Bytes |
|---|---|
| `expiry:suggest:7` | 17 |
| `expiry:suggest:365` | 19 |
| `bought:skip_expiry` | 20 |
| `price:skip_expiry` | 19 |

All well within Telegram's 64-byte limit.

---

## Localization Keys

| Key | EN | RU | PL |
|---|---|---|---|
| `expiry.suggest-days` | `{n} days` | `{n} дн.` | `{n} dni` |

Existing keys `bought.skip-expiry`, `shop.skip`, `shop.expiry-prompt`, `bought.expiry-prompt` are reused unchanged.

---

## Files Changed

| File | Change |
|---|---|
| `Services/ExpiryDaySuggestionService.cs` (new) | Suggestion logic |
| `Handlers/ExpirySuggestCallbackHandler.cs` (new) | `expiry:suggest:{days}` callback |
| `Repositories/PurchaseHistoryRepository.cs` | Two new query methods |
| `Handlers/BoughtStepHandler.cs` | Inject service; add suggestion buttons to step 1 reply |
| `Handlers/PriceCaptureStepHandler.cs` | Inject service; add suggestion buttons to step 2 reply; make `FinishDialogAsync` public |
| `Localization/Strings.en.json` | Add `expiry.suggest-days` |
| `Localization/Strings.ru.json` | Add `expiry.suggest-days` |
| `Localization/Strings.pl.json` | Add `expiry.suggest-days` |
| `Program.cs` | Register service and callback handler |

## AOT Compatibility

- No reflection or dynamic code generation.
- All SQL is static string literals.
- `ExpiryDaySuggestionService` uses constructor injection.
- No new `JsonSerializerContext` needed (no new JSON payloads).
