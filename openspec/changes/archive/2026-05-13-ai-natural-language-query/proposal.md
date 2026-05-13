## Why

Users have no way to ask questions about their shopping data — they can only view paginated lists and history entries. A natural language query interface lets users ask questions like "what did we buy most this month?" or "how much did we spend on dairy?" and get direct answers, without leaving Telegram.

## What Changes

- Add `/ai <question>` command that accepts a natural language question
- Add bot mention trigger (`@BotName <question>`) as an alias — detected in `UpdateDispatcher` before dialog handler check; bot username resolved at startup via `GetMeAsync()`
- Add `IDENTITY.md` file (checked into repo) that defines the AI's system prompt and schema context; loaded once at startup
- Add `AiQueryService` that runs two-round AI interaction: Round 1 generates SQL, Round 2 formats results as natural language
- SQL is executed on a read-only SQLite connection (`PRAGMA query_only = ON`); AI uses `@groupId` / `@chatId` placeholders; bot validates these are present before executing
- If the AI omits the placeholder, the query is rejected without execution and the user receives a localized error
- AI provider: OpenRouter (shared HTTP client infrastructure with `ai-calories-calc-photo` change)

## Capabilities

### New Capabilities

- `ai-natural-language-query`: Accept natural language questions via `/ai` command or bot mention; generate scoped SQL via AI (OpenRouter); execute read-only against SQLite; return AI-formatted natural language answer

### Modified Capabilities

- `telegram-bot-core`: Extend `UpdateDispatcher` to detect bot mentions in non-command messages and route them to the AI query handler

## Impact

- **Code**: New `AiCommandHandler`, `AiQueryService`, `IAiQueryService`; modified `UpdateDispatcher`; new `IDENTITY.md` at project root
- **APIs**: New OpenRouter HTTP call (text-only, no vision); two calls per user question
- **Dependencies**: `HttpClient` for OpenRouter (reused from `ai-calories-calc-photo` if that lands first; otherwise introduced here)
- **Systems**: SQLite accessed via additional read-only connection per query; no schema changes
- **Compatibility**: All AOT-safe — no reflection in JSON parsing; schema and IDENTITY.md loaded as embedded resource or file read at startup

## Rollback Plan

Remove `/ai` command registration and the mention-detection branch in `UpdateDispatcher`. No schema changes to revert. `IDENTITY.md` can be left in place harmlessly. No data is written by this feature.

## Affected Teams

Single-user/household bot. No shared infrastructure impact.

## Cross-Cutting Notes

- Follows existing `ICommandHandler` pattern for `/ai` command registration
- Mention routing added to `UpdateDispatcher` before `TryHandleDialogMessageAsync` — does not interfere with existing dialog state
- No user-facing strings hardcoded; all messages via `ILocalizer`
- No history audit entry needed (read-only, no state change)
- OpenRouter API key stored as environment variable (`OPENROUTER_API_KEY`), consistent with photo AI change
