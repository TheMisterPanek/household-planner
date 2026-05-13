## 1. IDENTITY.md and Configuration

- [x] 1.1 Create `IDENTITY.md` at project root containing the AI system prompt: bot persona, full SQLite schema (all tables and columns), instructions to use `@groupId`/`@chatId` placeholders, SELECT-only constraint, and `{groupId}`/`{chatId}` template variables for runtime substitution
- [x] 1.2 Add `OPENROUTER_API_KEY` and `AI_QUERY_MODEL` (default: `openai/gpt-oss-120b:free`) environment variable support; document in README and `.env.example`
- [x] 1.3 Add `AiQueryOptions` record/class to hold `ApiKey`, `Model`, `BaseUrl`, and `IdentityMdPath`; bind from `IConfiguration` in `Program.cs`

## 2. OpenRouter HTTP Client

- [x] 2.1 Create `IOpenRouterClient` interface with method: `Task<string?> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct)`
- [x] 2.2 Implement `OpenRouterClient` using `HttpClient` with OpenAI-compatible chat completions endpoint; serialize request/response using source-gen `JsonSerializerContext` (AOT-safe); 30s timeout
- [x] 2.3 Register `IOpenRouterClient` as `AddSingleton` with named `HttpClient` in `Program.cs`; inject `AiQueryOptions` for base URL, API key, model
- [x] 2.4 Add `OpenRouterRequestContext` and `OpenRouterResponseContext` to the source-gen `JsonSerializerContext` for AOT-safe serialization of request/response payloads

## 3. Bot Username Resolution

- [x] 3.1 Create `BotIdentityService` (singleton `IHostedService`) that calls `GetMeAsync()` on startup and stores the bot username; expose via `string BotUsername` property
- [x] 3.2 Register `BotIdentityService` in `Program.cs` as both `IHostedService` and singleton `BotIdentityService` so it can be injected into `UpdateDispatcher`

## 4. AI Query Service

- [x] 4.1 Create `IAiQueryService` interface with method: `Task<string> AnswerAsync(long chatId, long groupId, string question, CancellationToken ct)`
- [x] 4.2 Implement `AiQueryService`:
  - Load `IDENTITY.md` content at construction (inject path via `AiQueryOptions`)
  - Substitute `{groupId}` and `{chatId}` into the system prompt
  - Round 1: call `IOpenRouterClient.CompleteAsync` with schema prompt + question; extract SQL from response
  - Validate SQL contains `@groupId` or `@chatId` token; return localized error key if missing
  - Append `LIMIT 50` if the query does not already contain a LIMIT clause
  - Execute SQL on a new read-only SQLite connection (`PRAGMA query_only = ON`) with `@groupId` and `@chatId` bound as parameters
  - Format rows as pipe-delimited plain text; truncate at 3000 characters
  - Round 2: call `IOpenRouterClient.CompleteAsync` with results + original question; return natural language answer
- [x] 4.3 Handle `SqliteException` in query execution: catch, log at Warning, pass error note to Round 2
- [x] 4.4 Handle `HttpRequestException` / timeout in both AI calls: catch, log at Warning, return localized error key to handler
- [x] 4.5 Register `IAiQueryService` as `AddScoped` in `Program.cs`

## 5. /ai Command Handler

- [x] 5.1 Create `AiCommandHandler` implementing `ICommandHandler` with `Command = "/ai"`
- [x] 5.2 Extract question text: everything after `/ai ` (trim); if empty, send `ai.usage-hint` localized message and return
- [x] 5.3 Call `GroupRepository.GetOrCreateAsync` to resolve `GroupId`; call `IAiQueryService.AnswerAsync`; send result to user
- [x] 5.4 Register `AiCommandHandler` as `ICommandHandler` in `Program.cs`

## 6. Bot Mention Detection in UpdateDispatcher

- [x] 6.1 Inject `BotIdentityService` into `UpdateDispatcher` constructor
- [x] 6.2 In `HandleMessageAsync`, before calling `TryHandleDialogMessageAsync`, check `message.Entities` for any `MessageEntityType.Mention` whose text (extracted via `entity.Offset` + `entity.Length`) matches `@{BotUsername}` (case-insensitive)
- [x] 6.3 If mention found: strip all occurrences of `@{BotUsername}` from `message.Text`, trim to extract question, invoke `AiCommandHandler.HandleQueryAsync(message, question, cancellationToken)` and return
- [x] 6.4 Extract shared question-handling logic from `AiCommandHandler` into a method callable both from command handling and mention routing (avoid duplication)

## 7. Localization

- [x] 7.1 Add keys to `Strings.en.json`: `ai.usage-hint`, `ai.thinking`, `ai.error.unsafe-query`, `ai.error.service-unavailable`, `ai.error.no-result`
- [x] 7.2 Add Russian translations for all `ai.*` keys to `Strings.ru.json`
- [x] 7.3 Add Polish translations for all `ai.*` keys to `Strings.pl.json`

## 8. Unit Tests

- [x] 8.1 Unit tests for `AiQueryService.AnswerAsync`:
  - Happy path: Round 1 returns valid scoped SQL → executes → Round 2 formats → returns answer
  - Placeholder validation: SQL missing `@groupId`/`@chatId` → returns error key without executing
  - Empty result set: query returns 0 rows → Round 2 called with empty data → graceful answer
  - Round 1 failure (OpenRouter error) → returns error key, Round 2 not called
  - Round 2 failure (OpenRouter error) → returns fallback error key
  - SQL syntax error (SqliteException) → passes error note to Round 2, no rethrow
- [x] 8.2 Unit tests for `OpenRouterClient`: request construction (correct headers, model, messages), response parsing, timeout handling
- [x] 8.3 Unit tests for `AiCommandHandler`: empty question → usage hint; non-empty question → delegates to `IAiQueryService`
- [x] 8.4 Unit tests for `UpdateDispatcher` mention detection:
  - Message with bot mention entity → extracts question, routes to AI handler
  - Message with mention of another user → falls through to dialog handling
  - Message with bot mention but no question text → routes to AI handler (empty question handled there)
- [x] 8.5 Unit tests for `BotIdentityService`: username stored after `GetMeAsync` resolves

## 9. Manual Testing

- [ ] 9.1 Test `/ai what's on the list?` in group chat — verify answer reflects current shopping list
- [ ] 9.2 Test `/ai how many items did we buy this month?` — verify SQL executes and answer is correct
- [ ] 9.3 Test `/ai` with no text — verify usage hint message returned
- [ ] 9.4 Test `@BotName what did we buy last week?` mention — verify same result as `/ai` equivalent
- [ ] 9.5 Test mention mid-sentence: `hey @BotName summarize our purchases` — verify triggers correctly
- [ ] 9.6 Test with OpenRouter key missing/invalid — verify localized error, no crash
- [ ] 9.7 Verify existing commands (`/buy`, `/list`, `/history`) still work after `UpdateDispatcher` change
- [ ] 9.8 Verify dialog flows (buy step, price capture) are not interrupted by mention detection
