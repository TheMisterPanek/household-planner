# Testing Guide

This repo never talks to real Telegram in tests. Everything runs against a
mocked `ITelegramBotClient`. There are two test levels, both fast and fully
local — the only thing that touches real Telegram is the manual smoke test
step at the end of an OpenSpec change (see `CLAUDE.md`).

```
ProductTrackerBot.Tests/
  Handlers/      — unit tests: one handler, everything else mocked
  Services/      — unit tests for business logic / infra services
  Integration/   — "integration" tests: real dispatcher + real in-memory
                    SQLite + real DI wiring, only the Telegram client is mocked
```

Read this before writing tests for a new handler, callback, or dialog step.

## Why "integration" tests here are not e2e

They're called integration tests because they exercise the full in-process
path — `UpdateDispatcher` → handler lookup → handler → service → repository →
SQLite — the same wiring that runs in production. What's mocked is only the
network boundary: `ITelegramBotClient`. No real bot token, no Telegram
servers, no polling loop.

That's deliberate. Real e2e (actual Telegram chat, actual bot) is slow,
flaky, and manual — that's what the OpenSpec smoke-test task at the end of a
change is for. These mocked integration tests exist to catch the class of
bug a smoke test would otherwise catch — wrong handler wired up, wrong
callback prefix routed, DI registration missing, message not sent — but
catch it in CI in milliseconds instead of a human tapping through Telegram.
If a bug would only show up in the manual smoke test, that's a signal the
integration test suite has a gap — add a test here first.

Rule of thumb: **if you can express the bug as "dispatch this update, assert
this DB state / this outgoing request," write it as an integration test, not
just a unit test.** Unit tests catch logic bugs inside a handler; integration
tests catch wiring bugs between handlers, dispatcher, and DI.

## Level 1 — Handler/service unit tests (`Handlers/`, `Services/`)

One handler or service under test; every dependency is a `Mock<T>`.

- Build `Mock<ITelegramBotClient>`, stub `SendRequest` for whichever request
  types the handler issues (`SendMessageRequest`, `AnswerCallbackQueryRequest`,
  `EditMessageTextRequest`, ...), return `Task.FromResult(new Message())` /
  `true` as appropriate.
- Build the incoming `Update` / `CallbackQuery` / `Message` from raw JSON via
  `JsonSerializer.Deserialize<T>` with `PropertyNamingPolicy =
  JsonNamingPolicy.SnakeCaseLower` — this matches the real payload shape from
  Telegram.Bot and exercises real (de)serialization, not a hand-built object
  graph.
- Call the handler directly: `await handler.HandleAsync(update, ct)`.
- Assert with `Mock.Verify(...)` — repository/service methods called with the
  right args, `bot.SendRequest` called the right number of times with the
  right request type.
- Mocked `ILocalizer.Get(chatId, key)` returns `key` itself (the fallback
  convention from `CLAUDE.md`), so assertions can check for key names
  directly instead of translated text.

Use `Mock.Of<T>()` for a throwaway dependency with no behavior; `new
Mock<T>()` when you need `.Setup()`/`.Verify()`.

## Level 2 — Integration tests (`Integration/`)

All integration test classes derive from `TelegramIntegrationTestBase` and
carry `[Collection("IntegrationTests")]` so xUnit runs them serially against
the shared named in-memory SQLite connection
(`Data Source=file:integration_test?mode=memory&cache=shared`).

What the base class sets up once per test instance:

- Runs `DatabaseInitializer` against the in-memory DB — real schema, real
  repositories (`GroupRepository`, `ItemRepository`, `TagRepository`,
  `HistoryRepository`, ...). Nothing about the DB is mocked.
- Constructs **every** command handler, callback handler, and dialog step
  handler exactly as `Program.cs` would, wires them into a real
  `UpdateDispatcher` via a mocked `IServiceScopeFactory`/`IServiceProvider`
  (so DI resolution is exercised, not bypassed).
- One shared `Mock<ITelegramBotClient>` for the whole test — outgoing
  `SendMessageRequest`/`EditMessageTextRequest` calls are captured into lists
  instead of just counted, so tests can inspect the actual message/keyboard
  that would have been sent.
- `ILocalizer` is mocked the same way as in unit tests (`Get` returns the key).

### Writing a new integration test

1. `await ClearDataAsync();` first — always, even if you think the DB is
   already empty. Isolation depends on this.
2. Seed only what the test needs directly through the repositories
   (`GroupRepository.GetOrCreateAsync`, `ItemRepository.AddAsync`, ...) — not
   through dispatched updates, unless the update itself is what you're
   testing.
3. Build the update with a base-class helper:
   - `CommandUpdate(chatId, userId, text)` — `/command args`
   - `MessageUpdate(chatId, userId, text)` — plain text (dialog steps, free
     text replies)
   - `CallbackUpdate(chatId, userId, messageId, data)` — inline button tap
     (`callback_query` with `data`)
   - `PrivateCommandUpdate(...)` — same as `CommandUpdate` but `chat.type =
     "private"`, for handlers that restrict to private chats
4. `await DispatchAsync(update);`
5. Assert on two axes:
   - **DB state** — query the repository directly (`await
     ItemRepository.GetAllAsync(group.Id)`), don't re-derive state by
     re-dispatching another command.
   - **Outgoing bot traffic** — `GetLastSentMessage()` /
     `GetLastEditedMessage()` for the actual `SendMessageRequest`/
     `EditMessageTextRequest`, or `BotMock.Verify(...)` for a plain
     call-count check.

### Assert on content, not just "something was sent"

`Times.AtLeastOnce`/`Times.Once` on `It.IsAny<SendMessageRequest>()` only
proves the handler didn't crash and sent *some* message. It does not catch
"bot replied with the wrong text" or "right button was tapped but the
handler computed the wrong response" — both of these produce a passing test
under a call-count-only assertion.

Weak — passes even if the text is wrong:
```csharp
BotMock.Verify(b => b.SendRequest(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
```

Correct — pins down the actual content:
```csharp
BotMock.Verify(b => b.SendRequest(
    It.Is<SendMessageRequest>(r => r.Text == "buy.what-to-buy"), It.IsAny<CancellationToken>()), Times.Once);
// or, using the captured-message helpers:
Assert.Equal("buy.what-to-buy", GetLastSentMessage()!.Text);
```

Because the mocked `ILocalizer` returns the key itself, asserting
`r.Text == "some.key"` is exact and cheap — no need to hardcode translated
strings. For dynamic/interpolated text, use `r.Text.Contains(...)` or
`It.Is<SendMessageRequest>(r => r.Text.Contains("expected-substring"))`.

**Rule: any test whose purpose is "the bot replied correctly" or "tapping
this button produced the right reaction" must assert on `.Text` (and, for
keyboards, on `ReplyMarkup`/button `CallbackData`) — never on call count
alone.** Call-count-only assertions are acceptable only when the test's
actual purpose is narrower, e.g. "no message was sent for an unknown
callback" (`Times.Never`) or "exactly one confirmation was sent, content
covered by a different assertion in the same test."

Two failure modes this distinguishes, and how each is actually caught:

- **Wrong handler fired** (routing/prefix collision in `UpdateDispatcher`) —
  only an integration test dispatching through the real `UpdateDispatcher`
  can catch this; a unit test that calls `handler.HandleAsync(...)` directly
  never touches routing at all.
- **Right handler fired, wrong output** (logic bug inside the handler) —
  caught by either unit or integration test, but only if the assertion
  checks `.Text`/`ReplyMarkup` content as above.

### New handler checklist

If you add a new command handler, callback handler, or dialog step handler:

- Wire it into `TelegramIntegrationTestBase`'s constructor the same way the
  others are wired (construct it, add it to `commandHandlers` /
  `callbackHandlers` / `dialogHandlers`). If you skip this, every existing
  integration test still passes but your new handler is silently
  unreachable from `DispatchAsync` — the gap won't show up until the manual
  smoke test.
- Add at least one integration test that dispatches an update through it.

## Testing inline keyboards

The base class doesn't just count `SendMessageRequest` calls — it lets a
test read the actual keyboard the bot would render and then simulate a user
tapping a specific button, without hand-crafting `callback_data` strings.

Existing helpers, all following the same pattern (scan sent messages newest
→ oldest for an `InlineKeyboardMarkup`, return the first button whose
`CallbackData` matches a prefix):

- `GetLastBuyConfirmCallbackData()` → `"buy:confirm:"`
- `GetLastItemSaveCallbackData()` → `"item:save:"`
- `GetLastTagToggleCallbackData()` → `"tag:toggle:"`
- `GetLastExpirySuggestCallbackData()` → `"expiry:suggest:"`

If your new flow sends an inline keyboard the test needs to interact with,
add a matching `GetLast<X>CallbackData()` helper to
`TelegramIntegrationTestBase` following the same prefix-scan pattern, rather
than reconstructing the callback payload by hand in the test — the point is
to prove the button the bot actually sent has the data your handler expects,
not to assert two independently-written strings match.

Typical round-trip test shape:

```csharp
await ClearDataAsync();
var group = await GroupRepository.GetOrCreateAsync(chatId);

// 1. user sends a command that produces an inline keyboard
await DispatchAsync(CommandUpdate(chatId, userId, "/buy Milk"));
var confirmData = GetLastBuyConfirmCallbackData();
Assert.NotNull(confirmData);

// 2. user taps the button — simulate the resulting callback_query update
await DispatchAsync(CallbackUpdate(chatId, userId, 99, confirmData!));

// 3. assert DB changed and bot responded
var items = await ItemRepository.GetAllAsync(group.Id);
Assert.Contains(items, i => i.Name == "Milk");
```

This is the standard way to test a "bot sends keyboard → user taps → bot
reacts" flow end-to-end within the mocked world, and it's usually enough to
catch what a manual smoke test would catch — wrong callback prefix, stale
token, handler not wired into the dispatcher, wrong DB write.

## What still needs the manual OpenSpec smoke test

Mocked integration tests can't catch:

- Actual Telegram rendering quirks (message formatting, real keyboard
  layout, MarkdownV2 escaping edge cases as Telegram parses them)
- Real network/timeout/rate-limit behavior
- Anything that depends on a real bot token, real chat, or real Telegram
  client behavior

Everything else — routing, DI wiring, DB writes, correct callback data,
correct localization key usage — should be caught here first. Treat a bug
found only in the smoke test as a test-suite gap to close before moving on.
