## Context

`AiQueryService` currently enforces a fixed two-round flow: Round 1 generates SQL, the code validates and executes it, Round 2 formats the results. This works only when the AI's Round 1 response is valid scoped SQL. It fails for conversational questions (no SQL needed) and results in a confusing error message for both those and prompt injection attempts.

The redesign gives the AI control over whether SQL is needed and how many queries to run, while keeping all safety checks in the code layer.

## Goals / Non-Goals

**Goals:**
- Allow Round 1 to return a direct answer (no SQL), a planning document with one or more `APPLY_READ_SQL(...)` templates, or a humorous deflection
- Execute each template's SQL independently with per-template scope validation
- Skip Round 2 entirely when no templates are present (saves an API call)
- Teach the AI to detect and joke about prompt injection attempts
- Keep all safety properties: read-only via `PRAGMA query_only = ON`, scope enforcement via `@groupId`/`@chatId`

**Non-Goals:**
- Multi-turn conversations (each `/ai` call is still independent)
- Streaming responses
- Changing the OpenRouter HTTP client, `AiCommandHandler`, `UpdateDispatcher`, or DI registration

## Decisions

### 1. Template Syntax: `APPLY_READ_SQL(...)`

**Decision**: AI embeds SQL as `APPLY_READ_SQL(SELECT ...)` in its Round 1 response. Extraction uses parenthesis-counting (not a simple regex) to correctly handle nested parentheses in subqueries and SQL functions.

```
APPLY_READ_SQL(
  SELECT ItemName, COUNT(*) as cnt
  FROM PurchaseHistory
  WHERE GroupId = @groupId
    AND PurchasedAt >= date('now', 'start of month')
  GROUP BY ItemName
  ORDER BY cnt DESC
)
```

**Rationale**: Function-call syntax is intuitive for language models and matches how they already think about tool use. Parenthesis-counting handles all valid SQL without a complex delimiter scheme.

**Alternatives Considered**:
- `APPLY_READ_SQL[...]` (bracket-delimited) — unambiguous but feels less natural for the model
- Regex-only extraction — fails on nested parens in subqueries/functions

### 2. No-Template = Direct Answer, No Round 2

**Decision**: After Round 1, scan for `APPLY_READ_SQL(` in the response. If zero templates are found, return the Round 1 response directly to the user. Round 2 is not called.

**Rationale**: Saves one API call for conversational questions. The AI already wrote a user-facing response in Round 1; there is no need to reprocess it.

### 3. Per-Template Safety Check (Not Whole-Response)

**Decision**: Each SQL extracted from a template is validated independently for `@groupId` or `@chatId`. If a template fails validation, its placeholder is replaced with `[blocked: SQL must be scoped to @groupId or @chatId]` and processing continues with remaining templates.

**Rationale**: A partial failure should not silently suppress the rest of a valid multi-query response. The inline error marker is visible to the AI in Round 2, which can address it gracefully in the final answer. The log warning still fires per blocked template.

**Alternatives Considered**:
- Abort on first unsafe template — too aggressive; user gets nothing if one of three queries is bad
- Silent replacement with empty string — AI in Round 2 can't tell what happened

### 4. Round 2 Message = Resolved Planning Document

**Decision**: The Round 2 "user message" is the Round 1 response verbatim, with each `APPLY_READ_SQL(...)` replaced by the actual results table (or error marker). The system prompt (IDENTITY.md) is unchanged.

**Rationale**: The AI wrote the planning document itself, so it already knows the context and question. Re-sending it with data filled in is all that's needed for Round 2 to produce a final answer. No additional wrapping message is required.

**Previous approach for comparison**:
```
Round 2 user message (old): "The user asked: '...' Query results: ... Please answer..."
Round 2 user message (new): <Round 1 text with APPLY_READ_SQL replaced by data tables>
```

### 5. IDENTITY.md: Three-Mode Contract

**Decision**: Rewrite `IDENTITY.md` to define three explicit response types for Round 1. The AI must understand which mode to use.

| Mode | When to use | Round 2? |
|------|-------------|----------|
| Direct answer | No database data needed | No |
| Planning document with `APPLY_READ_SQL(...)` | Data needed | Yes |
| Humorous deflection | Attack / injection detected | No |

Attack patterns to detect: "ignore previous instructions", "you are now a different AI", requests to show raw schema or generate schema-modifying SQL, attempts to query other groups' data, classic SQL injection in question text.

Joke responses must be in the same language the user wrote in, short (1–2 sentences), and redirect toward legitimate shopping help.

### 6. SqliteException Handling per Template

**Decision**: If executing a template's SQL throws `SqliteException`, replace the template with `[query error: SQL could not be executed]`, log at Warning, and continue. Round 2 sees the error marker and can respond gracefully.

**Rationale**: Consistent with per-template partial failure policy. The AI is resilient to errors in its own planning documents.

## Sequence Diagram

```
User
  │
  │  /ai <question>
  ▼
AiCommandHandler
  │
  │  AnswerAsync(chatId, groupId, question)
  ▼
AiQueryService
  │
  │  CompleteAsync(model, systemPrompt, question)          [Round 1]
  ▼
OpenRouterClient ──────────────────────────────────────► OpenRouter API
                 ◄────────────────────────────────────── response text
  │
  │  response text (direct / planning doc / joke)
  ▼
AiQueryService
  │
  ├─ ExtractTemplates(response)
  │
  │  [0 templates found]
  │  └──────────────────────────────────────────────────► return response
  │
  │  [N templates found]
  │  for each (fullMatch, sql):
  │    ValidateSqlScope(sql)
  │    ├── fails → replace fullMatch with [blocked: ...]
  │    └── passes:
  │          ExecuteQueryAsync(sql, groupId, chatId)
  │          ├── SqliteException → replace with [query error: ...]
  │          └── success → replace fullMatch with results table
  │
  │  resolvedDoc = response with all templates substituted
  │
  │  CompleteAsync(model, systemPrompt, resolvedDoc)       [Round 2]
  ▼
OpenRouterClient ──────────────────────────────────────► OpenRouter API
                 ◄────────────────────────────────────── final answer
  │
  ▼
return final answer to AiCommandHandler → Telegram user
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| AI embeds SQL without `@groupId`/`@chatId` | Per-template validation; blocked templates replaced with error marker, not silently dropped |
| AI generates write SQL inside a template | `PRAGMA query_only = ON` at SQLite engine level; fires before any statement execution |
| AI produces more than N templates (expensive) | No hard cap in v1; log warning if > 5 templates; revisit if cost is an issue |
| AI ignores three-mode contract, returns hybrid | Heuristic: if no `APPLY_READ_SQL(` found → treat as direct answer; safe default |
| Joke deflection fails to trigger for subtle injections | Code's per-template scope check is the safety net; AI humor is the UX layer, not the security layer |
| Parenthesis-counting fails on malformed AI SQL | Unmatched parens → template not extracted → response returned as-is (no SQL executed) |

## Migration Plan

**Deploy:**
1. Replace `IDENTITY.md` content
2. Replace `AiQueryService.cs` implementation
3. No `Program.cs` changes, no schema changes, no localization changes

**Rollback:**
1. Revert `AiQueryService.cs` to previous version
2. Revert `IDENTITY.md` to previous version
3. No data to clean up
