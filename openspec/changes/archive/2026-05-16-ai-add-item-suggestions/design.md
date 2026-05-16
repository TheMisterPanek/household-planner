## Overview

The `/ai` command gains a suggestion layer: the AI can embed `ADD_ITEM(name, count)` markers in its response. `AiQueryService` parses these into structured `AiSuggestion` objects, `AiCommandHandler` renders them as inline keyboard buttons, and `AiAddItemCallbackHandler` adds the chosen item when tapped.

No new AI rounds or network calls are needed — marker parsing happens on the already-computed final answer.

---

## 1. ADD_ITEM Marker Syntax

The AI includes zero or more markers anywhere in its response text:

```
ADD_ITEM(pasta, 500g)
ADD_ITEM(eggs, 6)
ADD_ITEM(bacon)
```

Rules:
- First argument: item name (required). May contain spaces.
- Second argument: count/quantity (optional). Everything after the first comma, trimmed.
- Markers are stripped from the displayed text so the user never sees raw `ADD_ITEM(...)`.
- Parsing is case-insensitive (`add_item(...)` is also valid).

### Extraction regex (AOT-safe)

```csharp
[GeneratedRegex(@"ADD_ITEM\s*\(\s*(?<name>[^,)]+?)(?:\s*,\s*(?<count>[^)]+?))?\s*\)", RegexOptions.IgnoreCase)]
private static partial Regex AddItemRegex();
```

Extraction logic in `AiQueryService.ExtractSuggestions(string text)`:
- Find all matches.
- For each match: `Name = name.Trim()`, `Count = count group present ? count.Trim() : null`.
- Return `IReadOnlyList<AiSuggestion>`.

Strip logic in `AiQueryService.StripSuggestions(string text)`:
- Replace each full match (plus any surrounding whitespace/newline) with an empty string.
- Collapse multiple blank lines down to one.
- Trim the result.

---

## 2. System Prompt Injection

`AiQueryService` appends a fixed instruction block to the system prompt it builds from IDENTITY.md:

```
## Item Suggestion Syntax
When your answer naturally leads to specific shopping items (e.g. you suggest a recipe, a meal plan, or a restocking list), you MAY append ADD_ITEM markers to your response — one per item:

  ADD_ITEM(item name, quantity)
  ADD_ITEM(item name)

These will be rendered as "Add to list" buttons in Telegram. Rules:
- Only use them when suggesting concrete items the user would realistically want to buy.
- Do NOT use them for vague suggestions ("vegetables", "something sweet").
- Place markers at the end of your response, after the main text.
- Maximum 8 suggestions per response.
```

This block is appended in `BuildSystemPrompt(...)` regardless of whether IDENTITY.md already contains this instruction, so the AI always sees it.

---

## 3. Data Model

### `AiSuggestion`

```csharp
public sealed record AiSuggestion(string Name, string? Count);
```

### `AiQueryResult`

```csharp
public sealed record AiQueryResult(string Text, IReadOnlyList<AiSuggestion> Suggestions);
```

No serialization needed — both types are in-memory only.

---

## 4. IAiQueryService Interface Change

```csharp
Task<AiQueryResult> AnswerAsync(long chatId, long groupId, string question, string recentContext, CancellationToken ct);
```

Return type changes from `Task<string>` to `Task<AiQueryResult>`. All error paths return `new AiQueryResult(localizer.Get(...), [])` (empty suggestions list).

---

## 5. AiQueryService Changes

Two new internal helpers:

```csharp
internal static IReadOnlyList<AiSuggestion> ExtractSuggestions(string text)
internal static string StripSuggestions(string text)
```

In `AnswerAsync`, after the final answer string is obtained (Round 1 direct or Round 2):

```csharp
var suggestions = ExtractSuggestions(answer);
var cleanedText = suggestions.Count > 0 ? StripSuggestions(answer) : answer;
return new AiQueryResult(cleanedText, suggestions);
```

All existing error paths return `new AiQueryResult(errorMessage, [])`.

---

## 6. AiSuggestionService

```csharp
public sealed class AiSuggestionService
{
    private readonly Dictionary<string, AiSuggestion> _pending = new();

    public string Store(AiSuggestion suggestion)
    {
        var token = GenerateToken();
        _pending[token] = suggestion;
        return token;
    }

    public AiSuggestion? Get(string token) => _pending.GetValueOrDefault(token);

    public void Clear(string token) => _pending.Remove(token);

    private static string GenerateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
```

Registered as `AddSingleton<AiSuggestionService>` in `Program.cs`.

---

## 7. AiCommandHandler Changes

### Sequence — AI answer with suggestions

```
User: /ai what should I cook tonight?
↓
AiCommandHandler.ExecuteQueryAsync
  aiQueryService.AnswerAsync → AiQueryResult {
    Text = "I suggest pasta carbonara — it's quick and uses items you often buy.",
    Suggestions = [
      { Name="pasta", Count="500g" },
      { Name="eggs", Count="6" },
      { Name="bacon", Count="200g" },
      { Name="parmesan", Count=null }
    ]
  }

  For each suggestion:
    token = aiSuggestionService.Store(suggestion)
    row = [[InlineKeyboardButton("➕ pasta (500g)", callbackData="ai:add:a1b2c3d4")]]

  Bot → SendMessage(
    text = "I suggest pasta carbonara — it's quick and uses items you often buy.",
    replyMarkup = InlineKeyboardMarkup([row1, row2, row3, row4])
  )
```

### Sequence — AI answer without suggestions

```
User: /ai how many items are in my list?
↓
AiQueryResult { Text="You have 5 items.", Suggestions=[] }
Bot → SendMessage(text="You have 5 items.")   ← no replyMarkup (existing behavior)
```

### Button label format

| Suggestion | Button label |
|---|---|
| `{ Name="pasta", Count="500g" }` | `➕ pasta (500g)` |
| `{ Name="eggs", Count="6" }` | `➕ eggs (6)` |
| `{ Name="parmesan", Count=null }` | `➕ parmesan` |

Label built in handler: `count != null ? $"➕ {name} ({count})" : $"➕ {name}"`.

Each suggestion occupies one row in the inline keyboard (one button per row).

### Callback data

`ai:add:{token}` — e.g. `ai:add:a1b2c3d4` (16 bytes, within 64-byte limit ✓).

---

## 8. AiAddItemCallbackHandler

**Prefix**: `"ai:add:"`

```
Callback: ai:add:a1b2c3d4
↓
AiAddItemCallbackHandler
  token = callbackData["ai:add:".Length..]
  suggestion = aiSuggestionService.Get(token)
  if suggestion == null:
    AnswerCallbackQuery("Session expired, please ask again")
    return

  aiSuggestionService.Clear(token)
  group = groupRepository.GetOrCreateAsync(chatId)
  await itemRepository.AddAsync(group.Id, suggestion.Name, suggestion.Count, addedByName)
  AnswerCallbackQuery(localizer.Get(chatId, "ai.suggestion-added"))
  SendMessage(localizer.Get(chatId, "ai.suggestion-added-msg") formatted with name+count)
```

The `addedByName` is taken from `callbackQuery.From.FirstName` (same pattern as `BuyConfirmCallbackHandler`).

History recording follows the same try/catch pattern as `BuyConfirmCallbackHandler`.

---

## 9. Localization Keys

| Key | EN | Purpose |
|---|---|---|
| `ai.suggestion-added` | `✅ Added!` | `AnswerCallbackQuery` popup text |
| `ai.suggestion-added-msg` | `✅ Added: {item}` | Chat message (no count) |
| `ai.suggestion-added-msg-qty` | `✅ Added: {item} ({count})` | Chat message (with count) |
| `ai.suggestion-expired` | `Session expired. Please ask again.` | Expired token popup |

All keys added to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`.

---

## 10. Registration in Program.cs

```csharp
builder.Services.AddSingleton<AiSuggestionService>();
builder.Services.AddScoped<ICallbackHandler, AiAddItemCallbackHandler>();
```

`AiCommandHandler` already gets `IAiQueryService` injected — add `AiSuggestionService` to its constructor.

---

## 11. AOT Compatibility

- `AddItemRegex` uses `[GeneratedRegex]` attribute — AOT-safe.
- `AiQueryResult` and `AiSuggestion` are plain records — no serialization, no reflection.
- `AiSuggestionService` uses only `Dictionary<string, AiSuggestion>` and `RandomNumberGenerator` — AOT-safe.
- All new Telegram calls use existing `ITelegramBotClient` methods already present in the project.
