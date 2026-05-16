## 1. Add Localization Keys

- [x] 1.1 Add to `Strings.en.json`:
  - `ai.suggestion-added`: `"✅ Added!"`
  - `ai.suggestion-added-msg`: `"✅ Added: {item}"`
  - `ai.suggestion-added-msg-qty`: `"✅ Added: {item} ({count})"`
  - `ai.suggestion-expired`: `"Session expired. Please ask again."`
- [x] 1.2 Add the same keys with Russian translations to `Strings.ru.json`
- [x] 1.3 Add the same keys with Polish translations to `Strings.pl.json`

## 2. New Models

- [x] 2.1 Create `ProductTrackerBot/Models/AiSuggestion.cs` — `sealed record AiSuggestion(string Name, string? Count)`
- [x] 2.2 Create `ProductTrackerBot/Models/AiQueryResult.cs` — `sealed record AiQueryResult(string Text, IReadOnlyList<AiSuggestion> Suggestions)`

## 3. AiSuggestionService

- [x] 3.1 Create `ProductTrackerBot/Services/AiSuggestionService.cs` — singleton with `Store(AiSuggestion)→string`, `Get(token)→AiSuggestion?`, `Clear(token)`; token = 8-char lowercase hex from 4 random bytes (identical pattern to `PendingAddService`)
- [x] 3.2 Register as `AddSingleton<AiSuggestionService>()` in `Program.cs`

## 4. Update IAiQueryService

- [x] 4.1 Change return type of `AnswerAsync` from `Task<string>` to `Task<AiQueryResult>` in `IAiQueryService.cs`

## 5. Update AiQueryService

- [x] 5.1 Add `[GeneratedRegex(...)]` attribute and `AddItemRegex()` partial method for `ADD_ITEM\s*\(\s*(?<name>[^,)]+?)(?:\s*,\s*(?<count>[^)]+?))?\s*\)` (case-insensitive)
- [x] 5.2 Add internal static `ExtractSuggestions(string text) → IReadOnlyList<AiSuggestion>` — collects all regex matches into `AiSuggestion` records (Name = name group trimmed, Count = count group trimmed or null)
- [x] 5.3 Add internal static `StripSuggestions(string text) → string` — replaces all `ADD_ITEM(...)` matches (plus surrounding whitespace) with empty string; collapses multiple blank lines; trims result
- [x] 5.4 Append suggestion-syntax instruction block to the system prompt built in `AnswerAsync` (after IDENTITY.md content and optional recent-context block)
- [x] 5.5 After obtaining the final answer string, call `ExtractSuggestions` and `StripSuggestions`; return `new AiQueryResult(cleanedText, suggestions)` instead of the raw string
- [x] 5.6 Update all error return paths to return `new AiQueryResult(localizer.Get(chatId, "..."), [])` instead of bare strings
- [x] 5.7 Change method return type to `Task<AiQueryResult>`

## 6. Update AiCommandHandler

- [x] 6.1 Add `AiSuggestionService aiSuggestionService` parameter to constructor and store as field
- [x] 6.2 In `ExecuteQueryAsync`, update the call from `await aiQueryService.AnswerAsync(...)` (string) to `AiQueryResult result = await aiQueryService.AnswerAsync(...)`
- [x] 6.3 If `result.Suggestions.Count > 0`: for each suggestion, call `aiSuggestionService.Store(suggestion)` to get a token, build `InlineKeyboardButton("➕ {name} ({count})" or "➕ {name}", callbackData: $"ai:add:{token}")`, collect into one `InlineKeyboardMarkup` (one button per row)
- [x] 6.4 Send `result.Text` via `SendMessage`; include `replyMarkup` only when there are suggestions (otherwise send without markup, preserving current behavior)
- [x] 6.5 Record `result.Text` (cleaned text, without markers) to `conversationHistory`

## 7. AiAddItemCallbackHandler

- [x] 7.1 Create `ProductTrackerBot/Handlers/AiAddItemCallbackHandler.cs` implementing `ICallbackHandler`
- [x] 7.2 Register `AddScoped<ICallbackHandler, AiAddItemCallbackHandler>()` in `Program.cs`

## 8. Unit Tests — AiQueryService

- [x] 8.1 `ExtractSuggestions("...ADD_ITEM(pasta, 500g)...")` → `[{ Name="pasta", Count="500g" }]`
- [x] 8.2 `ExtractSuggestions("ADD_ITEM(eggs)")` → `[{ Name="eggs", Count=null }]`
- [x] 8.3 `ExtractSuggestions("No suggestions here.")` → empty list
- [x] 8.4 `ExtractSuggestions("ADD_ITEM(item one, 2 kg) ADD_ITEM(item two)")` → two suggestions
- [x] 8.5 `StripSuggestions("Here is the answer.\nADD_ITEM(pasta, 500g)\nADD_ITEM(eggs, 6)")` → `"Here is the answer."` (markers and surrounding blank lines removed)
- [x] 8.6 `AnswerAsync` mock: when AI returns text with `ADD_ITEM` markers, result `Text` has markers stripped and `Suggestions` list is populated
- [x] 8.7 `AnswerAsync` mock: when AI returns plain text (no markers), result `Text` equals the full response and `Suggestions` is empty

## 9. Unit Tests — AiCommandHandler

- [x] 9.1 When `AnswerAsync` returns result with suggestions: `SendMessage` is called with `replyMarkup` containing one button per suggestion; button labels include item name; `conversationHistory.Record` receives cleaned text (no markers)
- [x] 9.2 When `AnswerAsync` returns result with no suggestions: `SendMessage` is called without `replyMarkup` (preserves existing behavior)

## 10. Unit Tests — AiAddItemCallbackHandler

- [x] 10.1 Valid token: `ShoppingItemRepository.AddAsync` called with correct name and count; `AnswerCallbackQuery` called with "✅ Added!"; confirmation message sent
- [x] 10.2 Expired/missing token: `AnswerCallbackQuery` called with expired message; `AddAsync` NOT called
- [x] 10.3 Suggestion with no count: confirmation message uses the no-count key (`ai.suggestion-added-msg`)

## 11. Integration Tests

- [x] 11.1 Simulate `/ai` update where mocked `IAiQueryService.AnswerAsync` returns result with two suggestions; assert bot sends a message with `InlineKeyboardMarkup` containing two buttons with `ai:add:` prefix in callback data
- [x] 11.2 Simulate callback query `ai:add:{token}` (after storing a suggestion in `AiSuggestionService`); assert item is added to the repository and a confirmation message is sent
- [x] 11.3 Simulate callback query `ai:add:invalid`; assert no item is added and an expiry message is sent

## 12. Smoke Test

- [x] 12.1 Send `/ai suggest a quick pasta recipe` in a real Telegram chat; verify the bot replies with text AND inline buttons labelled `➕ <item name>` or `➕ <item name> (<count>)`
- [x] 12.2 Tap one of the suggestion buttons; verify `✅ Added!` popup appears and the item shows in `/list`
- [x] 12.3 Tap the same button again (expired token); verify an expiry message appears and no duplicate item is added
- [x] 12.4 Send `/ai how many items are in my list?` (a question with no items to suggest); verify the reply has NO inline buttons (plain text only)
- [x] 12.5 Verify `/list`, `/buy`, and other commands remain unaffected
