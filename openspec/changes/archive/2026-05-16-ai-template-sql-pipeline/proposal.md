## Why

The current AI query pipeline is SQL-first with no fallback: Round 1 always generates SQL, and if the SQL lacks `@groupId`/`@chatId` scoping placeholders, the user receives a generic "failed to form a safe query" error. This breaks in two common situations:

1. **Non-data questions** — "what should I cook tonight?" or "how should I store tomatoes?" don't need the database at all. The AI can answer directly, but the current code rejects the response because it looks like prose rather than SQL.
2. **Prompt injection** — attempts to override instructions or extract cross-group data result in the same confusing error instead of a graceful, on-brand response.

The fix is to flip the model: instead of the code prescribing "Round 1 = SQL", the AI decides what it needs and embeds zero or more `APPLY_READ_SQL(...)` templates in its response. The code resolves those templates, then returns to the AI for a final answer — or, if there were no templates, returns the Round 1 response directly.

## What Changes

- **`AiQueryService`**: Replace the fixed 2-round SQL-first flow with a template resolution loop. Round 1 returns a "planning document" that may contain any number of `APPLY_READ_SQL(SELECT ...)` placeholders. Code extracts each, validates scope, executes read-only against SQLite, and substitutes results in-place. If no templates are present, the Round 1 response is the final answer (no Round 2 call). If templates are present, the resolved document is sent to Round 2 for a final natural language answer.
- **`IDENTITY.md`**: Rewrite to teach the AI three response modes: (1) direct answer when no data is needed, (2) planning document with `APPLY_READ_SQL(...)` templates when data is needed, and (3) humorous deflection when a prompt injection or jailbreak attempt is detected.
- **Safety model**: Scope validation moves from a whole-response check to a per-template check. Each SQL inside a template must contain `@groupId` or `@chatId`; those that don't are replaced with an inline error marker rather than aborting the whole request. `PRAGMA query_only = ON` still enforces read-only at the SQLite engine level.

## Capabilities

### Modified Capabilities

- `ai-natural-language-query`: Replace fixed SQL-first round-trip with template-based resolution; enable direct answers for non-data questions; enable humorous deflection for attacks; allow multiple SQL queries per user question.

## Impact

- **Code**: `AiQueryService` internals rewritten; `IDENTITY.md` rewritten; no changes to `AiCommandHandler`, `UpdateDispatcher`, or any repository/handler
- **APIs**: Fewer OpenRouter calls for non-data questions (no Round 2); same or more calls for complex multi-query questions
- **Dependencies**: No new dependencies
- **Systems**: No schema changes; SQLite read path unchanged
- **Compatibility**: Fully AOT-safe; no reflection; same `IOpenRouterClient` and `ILocalizer` interfaces

## Rollback Plan

`AiQueryService` is an internal implementation detail behind `IAiQueryService`. Rolling back means reverting `AiQueryService.cs` and `IDENTITY.md` to their previous versions. `AiCommandHandler`, `UpdateDispatcher`, and `Program.cs` are untouched by this change and need no rollback.

## Affected Teams

Single-user/household bot. No shared infrastructure impact.

## Cross-Cutting Notes

- No user-facing strings are hardcoded; localization keys are unchanged (`ai.error.unsafe-query` is retired from the main flow but the key is kept in `Strings.*.json`)
- No state-changing operations; `IHistoryRepository.RecordAsync` is not called (read-only feature)
- Follows existing `IAiQueryService` interface; no changes to DI registration
- `PRAGMA query_only = ON` remains as the deepest defense layer regardless of template validation outcome
