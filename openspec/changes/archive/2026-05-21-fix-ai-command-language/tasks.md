## 1. Update `IDENTITY.md`

- [x] 1.1 In `ProductTrackerBot/IDENTITY.md`, find the line:
  ```
  - Язык ответа — тот, на котором написали (русский/польский/английский).
  ```
  Replace it with:
  ```
  - Always respond in: **{primaryLanguage}**. Even if the user writes in a different language, reply in {primaryLanguage} unless they explicitly ask you to switch.
  ```
- [x] 1.2 Verify the file still has `{groupId}` and `{chatId}` placeholders intact.

---

## 2. Add `LanguageNames` helper

- [x] 2.1 Create `ProductTrackerBot/Localization/LanguageNames.cs`:
  ```csharp
  public static class LanguageNames
  {
      private static readonly Dictionary<string, string> Names = new()
      {
          { "en", "English" },
          { "ru", "Russian" },
          { "pl", "Polish" },
      };

      public static string GetDisplayName(string? languageCode)
          => Names.TryGetValue(languageCode ?? string.Empty, out var name)
              ? name
              : "English";
  }
  ```
- [x] 2.2 Run `dotnet build` — confirm 0 errors.

---

## 3. Update `IAiQueryService`

- [x] 3.1 Add `string language` parameter after `recentContext` in `AnswerAsync`:
  ```csharp
  Task<AiQueryResult> AnswerAsync(
      long chatId,
      long groupId,
      string question,
      string recentContext,
      string language,
      CancellationToken ct);
  ```

---

## 4. Update `AiQueryService`

- [x] 4.1 Add `string language` parameter to `AnswerAsync` signature.
- [x] 4.2 Add `.Replace("{primaryLanguage}", language, StringComparison.Ordinal)` to the system prompt substitution chain (after the `{chatId}` replacement).
- [x] 4.3 Run `dotnet build` — confirm 0 errors (compiler will surface all callers that need updating).

---

## 5. Update `AiCommandHandler`

- [x] 5.1 In `ExecuteQueryAsync`, after `var group = await this.groupRepository.GetOrCreateAsync(...)`, add:
  ```csharp
  var language = LanguageNames.GetDisplayName(group.LanguageCode);
  ```
- [x] 5.2 Pass `language` as the new fifth argument to `this.aiQueryService.AnswerAsync(...)`.
- [x] 5.3 Run `dotnet build` — confirm 0 errors.

---

## 6. Update unit tests — `AiCommandHandlerTests`

- [x] 6.1 Open `ProductTrackerBot.Tests/Handlers/AiCommandHandlerTests.cs`.
- [x] 6.2 For every mock setup / verify call on `IAiQueryService.AnswerAsync`, add the `language` argument (use `It.IsAny<string>()` or a specific value where the test cares about it).
- [x] 6.3 Add a test: group has `LanguageCode = "en"` → `AnswerAsync` is called with `language = "English"`.
- [x] 6.4 Add a test: group has `LanguageCode = "ru"` → `AnswerAsync` is called with `language = "Russian"`.
- [x] 6.5 Add a test: group has `LanguageCode = "pl"` → `AnswerAsync` is called with `language = "Polish"`.
- [x] 6.6 Add a test: group has `LanguageCode = null` → `AnswerAsync` is called with `language = "English"` (fallback).
- [x] 6.7 Run `dotnet test` — confirm all tests pass.

---

## 7. Update unit tests — `AiQueryService` (if test file exists)

- [x] 7.1 Find any direct tests of `AiQueryService.AnswerAsync` and add the `language` argument.
- [x] 7.2 Add a test: system prompt after substitution contains the `language` value and does NOT contain the literal `{primaryLanguage}` placeholder.
- [x] 7.3 Run `dotnet test` — confirm all tests pass.

---

## 8. Integration test

- [x] 8.1 Open or create `ProductTrackerBot.Tests/Integration/AiCommandIntegrationTests.cs`.
- [x] 8.2 Seed a group with `LanguageCode = "en"`.
- [x] 8.3 Simulate `/ai what's on the list?` update dispatch.
- [x] 8.4 Capture the `CompleteAsync` call arguments on the mocked `IOpenRouterClient`; assert the system prompt contains `"English"` and does not contain `"{primaryLanguage}"`.
- [x] 8.5 Repeat for `LanguageCode = "ru"` → assert system prompt contains `"Russian"`.
- [x] 8.6 Run `dotnet test` — confirm all integration tests pass.

---

## 9. Smoke test (manual e2e — do NOT mark complete until confirmed by user)

Perform in a real Telegram group chat with the bot running:

1. Run `/settings` and select **English** as the primary language.
2. Send `/ai что у нас на листе покупок?` (question written in Russian).
3. Verify the bot replies **in English** (not Russian).
4. Run `/settings` and switch to **Russian**.
5. Send `/ai what's on the shopping list?` (question in English).
6. Verify the bot replies **in Russian**.
7. Run `/settings` and switch to **Polish**.
8. Send `/ai what should we cook?` (question in English).
9. Verify the bot replies **in Polish**.
