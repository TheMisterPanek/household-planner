## Why

The `/ai` command answers questions and can suggest meals, recipes, or shopping ideas — but the user has no way to act on those suggestions without manually typing `/buy` for each item. If the AI says "try pasta carbonara this week," adding the ingredients requires 4–5 separate commands.

Adding a suggestion mechanism lets the AI embed `ADD_ITEM(name, count)` markers in its response. The bot detects these, strips them from the display text, and renders inline buttons so the user can add items with a single tap — bridging the gap between AI advice and list management.

## What Changes

- **`AiQueryResult`** (new model): `{ string Text, IReadOnlyList<AiSuggestion> Suggestions }` — structured return type replacing the plain `string` from `AnswerAsync`.
- **`AiSuggestion`** (new model): `{ string Name, string? Count }` — one suggested shopping item.
- **`IAiQueryService`**: Return type changed from `Task<string>` to `Task<AiQueryResult>`.
- **`AiQueryService`**: Parses `ADD_ITEM(name, count)` markers from the final answer text; strips them from the displayed text; appends suggestion-syntax instructions to the system prompt so the AI knows it can use them.
- **`AiSuggestionService`** (new singleton): Stores `(Name, Count)` by short token; same Store/Get/Clear token pattern as `PendingAddService`.
- **`AiCommandHandler`**: After receiving `AiQueryResult`, stores each suggestion in `AiSuggestionService` and renders inline keyboard buttons (one row per suggestion) alongside the answer text.
- **`AiAddItemCallbackHandler`** (new handler): Handles `ai:add:{token}` callbacks; looks up the suggestion, calls `ShoppingItemRepository.AddAsync`, sends a localized confirmation.
- **New localization keys**: suggestion button label, item-added confirmation, empty-suggestion fallback.

## Capabilities

### Modified Capabilities

- `ask-ai-question`: AI answers now optionally carry item suggestions rendered as tappable "Add to list" buttons.

## Impact

- **Code**: 2 new model files; 1 new service; 1 new callback handler; 4 existing files modified (`IAiQueryService`, `AiQueryService`, `AiCommandHandler`, `Program.cs`).
- **APIs**: No new external APIs. Uses existing `ShoppingItemRepository.AddAsync`.
- **Dependencies**: No new NuGet packages.
- **Systems**: No schema changes.
- **Compatibility**: Fully AOT-safe — no reflection on new types; `AiQueryResult` is not serialized. Callback tokens follow the existing 8-char hex pattern; `ai:add:a1b2c3d4` = 16 bytes, well within the 64-byte Telegram limit.

## Rollback Plan

All new code is additive. Rollback:
1. Revert `IAiQueryService` return type and `AiQueryService` to the plain `string` path.
2. Revert `AiCommandHandler` to call `SendMessage` directly with the string answer.
3. Remove `AiSuggestionService`, `AiAddItemCallbackHandler`, `AiQueryResult`, `AiSuggestion`.
4. Remove their `Program.cs` registrations and new localization keys.

## Cross-Cutting Notes

- `ADD_ITEM` markers are stripped before the text reaches the user — the raw syntax never appears in chat.
- If the AI returns no `ADD_ITEM` markers, the behavior is identical to the current implementation.
- Suggestions are stored in-memory only; tokens expire on process restart (acceptable).
- `AiSuggestionService` is registered as `AddSingleton` (same as `PendingAddService`).
- Suggestion buttons appear only when the AI includes `ADD_ITEM` markers; no buttons are rendered for plain answers.
- Group context for the added item comes from `GroupRepository.GetOrCreateAsync` inside the callback handler, matching the existing `BuyConfirmCallbackHandler` pattern.
