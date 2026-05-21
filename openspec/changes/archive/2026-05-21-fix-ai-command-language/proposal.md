# Proposal: Fix `/ai` Command to Respect Primary Language from `/settings`

## What & Why

When a user sends `/ai` the bot always responds in Russian, regardless of the language configured in `/settings`. Users who set their primary language to English or Polish still receive Russian responses from the AI.

The root cause: `IDENTITY.md` — the system prompt fed to the AI model — is written in Russian and instructs the AI to respond in *"the language the user typed in"*. It has no knowledge of the group's `LanguageCode` stored in the database and set via `/settings`.

Bot UI messages (shopping list, menus, expiry notifications) already respect `LanguageCode` through `ILocalizer`. The AI assistant should follow the same convention.

## Proposed Solution

Inject the group's primary language into the AI system prompt so the AI knows what language to respond in by default.

### Changes

1. **`IDENTITY.md`** — Replace the current hard-coded language instruction with a `{primaryLanguage}` placeholder, e.g.:
   > "Respond in: **{primaryLanguage}**. If the user writes in a different language, still reply in {primaryLanguage} unless they explicitly ask you to switch."

2. **`IAiQueryService`** — Add a `string language` parameter to `AnswerAsync`. This keeps the service testable without a DB lookup.

3. **`AiQueryService.AnswerAsync`** — Replace `{primaryLanguage}` in `identityTemplate` with the `language` argument.

4. **`AiCommandHandler.ExecuteQueryAsync`** — The handler already calls `groupRepository.GetOrCreateAsync`, which returns the group including `LanguageCode`. Pass `group.LanguageCode` to `AnswerAsync`.

### Language Name Mapping

The AI model interprets language codes best when given a human-readable name. A small static helper converts `"en"` → `"English"`, `"ru"` → `"Russian"`, `"pl"` → `"Polish"` before injection.

## Affected Components

| Component | Change |
|---|---|
| `IDENTITY.md` | Replace language instruction with `{primaryLanguage}` placeholder |
| `IAiQueryService` | Add `string language` parameter to `AnswerAsync` |
| `AiQueryService` | Accept and inject `language` into system prompt |
| `AiCommandHandler` | Pass `group.LanguageCode` (resolved to display name) to `AnswerAsync` |
| `AiCommandHandlerTests` | Update tests with new `language` parameter |
| `AiQueryServiceTests` (if any) | Update mocks/calls with new parameter |

## Rollback Plan

Revert `IDENTITY.md`, `IAiQueryService.cs`, `AiQueryService.cs`, and `AiCommandHandler.cs` to their previous versions. No database or schema changes — purely application-level.

## Cross-Cutting Notes

- Fallback: if `LanguageCode` is null or unknown, default to `"en"` (English) — same convention as `SupportedLanguages.DefaultLanguage`.
- No new packages, no DB migrations.
- The change has no effect on `AiMealSuggestionService` (that service builds its own prompt and uses the meal library, not `IDENTITY.md`).
