## Context

The bot currently has no query interface. Users can view paginated shopping lists and recent history, but cannot ask questions across their data — e.g., "what did we buy most last month?" or "how much did we spend at Lidl?". Adding a natural language query layer requires an AI model to translate questions into SQL, an execution layer with strict read-only enforcement, and a second AI call to format raw rows into a friendly answer.

The existing dispatch architecture (`ICommandHandler`, `ICallbackHandler`, `IDialogMessageHandler`, `UpdateDispatcher`) handles the routing cleanly. The key new concern is trusting AI-generated SQL safely.

## Goals / Non-Goals

**Goals:**
- Accept natural language questions via `/ai` command and bot mention (`@BotName`)
- Translate questions to SQL using OpenRouter; execute read-only against SQLite; format results with a second AI call
- Enforce read-only access at the SQLite engine level (`PRAGMA query_only = ON`)
- Enforce chat-scoping via mandatory `@groupId` / `@chatId` placeholders; reject queries missing them
- Keep OpenRouter integration reusable (shared `HttpClient` with `ai-calories-calc-photo`)
- No schema changes; pure read path

**Non-Goals:**
- Multi-turn AI conversations (each `/ai` call is independent)
- Write operations or database modifications of any kind
- Querying data across chats (always scoped to current chat's GroupId/ChatId)
- Caching or persisting AI responses
- Support for AI providers other than OpenRouter in v1

## Decisions

### 1. Two-Round AI Interaction

**Decision**: Use two separate AI calls per user question.

- **Round 1**: System prompt (IDENTITY.md content + schema + current GroupId/ChatId) + user question → AI returns a SQL SELECT query using `@groupId`/`@chatId` placeholders
- **Round 2**: Round 1 system prompt + original question + SQL results (as formatted rows) → AI returns a natural language answer

**Rationale**: Separating SQL generation from answer formatting keeps each AI call focused. It also allows the bot to intercept and validate the SQL between rounds — something not possible with single-call tool-use where the AI executes the tool directly. Two calls is also simpler to implement than a full tool-use loop and avoids the need for function-calling support in the OpenRouter model.

**Alternatives Considered**:
- *Tool use / function calling*: AI calls a `run_query(sql)` tool, bot executes, returns rows in one conversation. More powerful and natural, but requires a model that supports tool use, adds framework complexity, and makes it harder to intercept/validate the SQL before execution. Good V2 option.
- *Single call (send all data as context)*: Fetch all chat data upfront and send as context; AI answers directly. Simpler but breaks for large histories; context window cost scales with data size. Not appropriate when history could span years.

### 2. SQL Scoping: Parameterized Placeholders + Validation

**Decision**: Instruct the AI to use `@groupId` and `@chatId` as named placeholders in WHERE clauses. Before execution, the bot validates the generated SQL contains at least one of these tokens. If missing, the query is rejected without execution.

**Rationale**: The bot cannot trust that the AI will always remember to scope the query. Parameterized values prevent the AI from embedding wrong IDs, and the validation check catches the case where scoping is omitted entirely. This is simple to implement and gives a clear, auditable failure mode.

**Execution**: `PRAGMA query_only = ON` is set on the SQLite connection before executing the AI-generated SQL. This is the strongest possible guardrail — even if validation is somehow bypassed, SQLite refuses any write operation at the engine level.

**Alternatives Considered**:
- *Scoped temporary views*: Create `TEMP VIEW ShoppingItems AS SELECT * FROM ShoppingItems WHERE GroupId = X` so the AI never needs to scope manually. Stronger isolation but requires the same connection for view creation and query execution; adds complexity and unusual SQLite usage patterns.
- *SQL AST parsing*: Parse the SQL and inject WHERE clauses programmatically. Fragile, complex, and overkill for v1.

### 3. IDENTITY.md as System Prompt File

**Decision**: Store the AI system prompt (persona, instructions, full schema definition) in `IDENTITY.md` at the project root. Load it once at startup via file read; inject the resolved `groupId` and `chatId` per request.

**Rationale**: A file-based system prompt can be edited and iterated without recompiling or redeploying. The schema definition and instructions for the AI are editorial content — they don't belong hardcoded in C# strings. `IDENTITY.md` is checked into the repo and treated like configuration.

**Format**: Markdown, but the AI receives it as plain text. Template variables `{groupId}` and `{chatId}` are substituted at request time using `string.Replace`.

### 4. Mention Detection in UpdateDispatcher

**Decision**: Detect bot mentions in `UpdateDispatcher.HandleMessageAsync` before the existing dialog handler check. Bot username is resolved once at startup via `ITelegramBotClient.GetMeAsync()` and stored in a singleton options object.

```
HandleMessageAsync:
  1. Starts with '/'       → command routing (existing)
  2. Mentions @BotName     → extract question, route to AiCommandHandler.HandleQueryAsync
  3. Otherwise             → dialog routing (existing)
```

**Mention extraction**: Check `message.Entities` for `MessageEntityType.Mention` entries. For each, extract the username from the text using `entity.Offset` and `entity.Length`. If any entity matches the bot's username, strip all `@BotName` occurrences from the message text and trim to get the question.

**Rationale**: Mentions can appear anywhere in a message ("hey @BotName what did we buy?"), so entity-based detection is more robust than prefix matching. Reusing `AiCommandHandler` logic avoids duplication.

### 5. OpenRouter as AI Provider

**Decision**: Use OpenRouter via a plain `HttpClient` POST to the OpenRouter chat completions API (OpenAI-compatible). Reuse the `HttpClient` registration that `ai-calories-calc-photo` introduces; if that change is not yet merged, register it here.

**Rationale**: OpenRouter gives model flexibility (can swap between Claude, GPT-4o, Gemini) without code changes. API key stored in `OPENROUTER_API_KEY` env var. Text-only models are sufficient (no vision needed), so a cheaper/faster model can be selected.

**Model selection**: Configured via `AI_QUERY_MODEL` env var, defaulting to `openai/gpt-oss-120b:free`. Documented in README.

### 6. Result Row Limit and Truncation

**Decision**: Append `LIMIT 50` to all AI-generated queries (or enforce via a wrapper if the AI includes its own LIMIT). Format results as a plain-text table (pipe-delimited) before sending to Round 2. If formatted results exceed 3000 characters, truncate with a note.

**Rationale**: Prevents runaway queries from producing enormous result sets. 50 rows is sufficient for any natural language question; if the user needs more they can ask a more specific question.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| AI omits `@groupId`/`@chatId` placeholder | Validate before execution; reject with localized error |
| AI generates invalid SQL (syntax error) | Catch `SqliteException`; pass empty result + error description to Round 2; AI replies gracefully |
| AI generates write SQL (INSERT/DELETE) | `PRAGMA query_only = ON` blocks at engine level regardless |
| OpenRouter unavailable or rate-limited | Catch HTTP errors; reply with localized "AI service unavailable" error; log at Warning |
| Large result sets overwhelming Round 2 context | Hard `LIMIT 50` + character truncation on formatted results |
| AI generates cross-chat query (missing WHERE) | Validation check + placeholder enforcement; second layer: `PRAGMA query_only` prevents writes but not cross-chat reads — placeholder validation is the primary guard |
| Mention detection false-positives | Only trigger if entity type is `Mention` (not `TextMention`); verify username match, not just presence of `@` |

## Migration Plan

**Deploy:**
1. Add `IDENTITY.md` to project root (created as part of this change)
2. Register `AiCommandHandler` and `AiQueryService` in `Program.cs`
3. Add `OPENROUTER_API_KEY` and `AI_QUERY_MODEL` to `.env` / deployment config
4. Modify `UpdateDispatcher` to detect mentions (requires bot username at construction time)
5. Deploy — no schema changes, no data migration

**Rollback:**
1. Remove `AiCommandHandler` registration from `Program.cs`
2. Revert `UpdateDispatcher` mention-detection branch
3. No data to clean up (read-only feature)

## Open Questions

1. **Private chat mentions**: Should `@BotName question` work in a private chat (DM with bot)? In private chats, messages don't need the `@` prefix to reach the bot, so mentions are less common. Recommendation: support it — no special-casing needed since the detection logic works identically.
2. **Empty question handling**: If user sends `/ai` with no text, or mentions the bot with no question, should the bot prompt for input or reply with usage instructions? Recommendation: reply with a short usage hint (localized).
3. **Round 2 model**: Should Round 1 (SQL generation) and Round 2 (formatting) use the same model? They could differ — a smarter model for SQL, a cheaper one for formatting. Keep same model for v1 simplicity; split via config in V2.
