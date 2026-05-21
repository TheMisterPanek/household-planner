# Design: Fix `/ai` Command to Respect Primary Language from `/settings`

## Overview

The AI assistant always responds in Russian because `IDENTITY.md` is authored in Russian and contains no per-chat language configuration. This change threads the group's `LanguageCode` (set via `/settings`) through the call stack so the AI system prompt includes an explicit response-language instruction.

---

## Root Cause

`AiQueryService.AnswerAsync` builds the system prompt from `IDENTITY.md` and substitutes only `{groupId}` and `{chatId}`. The group's `LanguageCode` is never read or injected. The current `IDENTITY.md` says:

> "Язык ответа — тот, на котором написали (русский/польский/английский)."

This means the AI mirrors whatever language the user *typed* in — not the language they *configured*. For users who set English or Polish and then type a Russian query (e.g. from a bilingual household), the bot replies in Russian.

---

## Data Flow (After Fix)

```
AiCommandHandler.HandleAsync
  │
  ├─ groupRepository.GetOrCreateAsync(chatId)  ← already called; group.LanguageCode = "en" / "ru" / "pl"
  │
  ├─ LanguageNames.GetDisplayName(group.LanguageCode)  ← "English" / "Russian" / "Polish"
  │
  └─ aiQueryService.AnswerAsync(chatId, group.Id, question, context, language, ct)
                                                                        ↑ NEW
AiQueryService.AnswerAsync
  │
  └─ systemPrompt = identityTemplate
       .Replace("{groupId}", ...)
       .Replace("{chatId}", ...)
       .Replace("{primaryLanguage}", language)   ← NEW substitution
```

---

## IDENTITY.md Change

Replace the current language line:

```
- Язык ответа — тот, на котором написали (русский/польский/английский).
```

With a placeholder-driven instruction placed in the **Тон общения** / tone section:

```
- Always respond in: **{primaryLanguage}**. Even if the user writes in a different language, reply in {primaryLanguage} unless they explicitly ask you to switch.
```

The rest of `IDENTITY.md` remains in Russian (it is a valid system prompt language; the AI can follow Russian instructions and produce output in any language).

---

## `LanguageNames` Helper

New small static class in `ProductTrackerBot/Localization/`:

```csharp
// Localization/LanguageNames.cs
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
            : "English"; // fallback matches SupportedLanguages.DefaultLanguage
}
```

---

## Interface Change

```csharp
// IAiQueryService.cs
Task<AiQueryResult> AnswerAsync(
    long chatId,
    long groupId,
    string question,
    string recentContext,
    string language,      // ← new: display name e.g. "English"
    CancellationToken ct);
```

---

## `AiQueryService` Change

In `AnswerAsync`, add one substitution after the existing `{chatId}` replacement:

```csharp
var systemPrompt = this.identityTemplate
    .Replace("{groupId}", groupId.ToString(), StringComparison.Ordinal)
    .Replace("{chatId}",  chatId.ToString(),  StringComparison.Ordinal)
    .Replace("{primaryLanguage}", language,   StringComparison.Ordinal);  // ← new
```

---

## `AiCommandHandler` Change

`ExecuteQueryAsync` already resolves the group; pass the language display name:

```csharp
var group = await this.groupRepository.GetOrCreateAsync(message.Chat.Id);
var language = LanguageNames.GetDisplayName(group.LanguageCode);
result = await this.aiQueryService.AnswerAsync(
    message.Chat.Id, group.Id, question, recentContext, language, cancellationToken);
```

---

## Error Handling / Edge Cases

| Scenario | Behaviour |
|---|---|
| `group.LanguageCode` is null | `LanguageNames.GetDisplayName` returns `"English"` |
| Unknown language code (future addition not yet in `LanguageNames`) | Falls back to `"English"` |
| `IDENTITY.md` has no `{primaryLanguage}` placeholder (misconfiguration) | Template substitution is a no-op; behaviour unchanged from today |

---

## Schema Impact

None. No DB changes required.

---

## AOT / Build Compatibility

- `LanguageNames` is a static class with a plain `Dictionary<string, string>` — no reflection.
- No new NuGet packages.
- Interface signature change requires updating all callers and mocks; covered in tasks.
