## Context

The bot currently outputs all user-facing text as hardcoded Russian string literals scattered across command and callback handlers (`BuyCommandHandler`, `ShoppingListService`, `BuyStepHandler`, etc.). There is no abstraction for user-facing strings.

The `Groups` table in SQLite holds one row per Telegram chat with `ChatId` and `ListMessageId`. It has no language preference. Every response is constructed inline with Russian text, making adding any second language a wide-reaching change.

The project compiles with AOT support (trimming enabled, no reflection-heavy patterns). Any i18n approach must be AOT-compatible.

## Goals / Non-Goals

**Goals:**
- Per-chat language preference stored in SQLite alongside the existing `Groups` record
- A single `ILocalizer` abstraction that all handlers use to resolve strings by key and locale
- Translation data loaded from embedded JSON files at startup using source-generated deserialization (AOT-safe)
- A `/language` command with an inline keyboard so users can change their chat's language
- Initial supported locales: English (`en`) and Russian (`ru`)

**Non-Goals:**
- Per-user (not per-chat) language preferences
- Dynamic translation loading at runtime (no hot reload of translations)
- Pluralization rules or date/number formatting beyond simple key-value strings
- Automatic language detection from Telegram's `language_code` field
- Third-party i18n library adoption

## Decisions

### Decision 1: Embedded JSON translation files over .resx or generated code

**Chosen**: Embedded JSON files (`Strings.en.json`, `Strings.ru.json`) deserialized into `Dictionary<string, string>` at startup using `System.Text.Json` with a source-generated `JsonSerializerContext`.

**Alternatives considered**:
- `.resx` files with `ResourceManager`: uses runtime reflection, not AOT-safe without extra MSBuild configuration.
- Hardcoded switch/dictionary in C#: works for AOT but is harder to maintain and diff when adding locales.
- Third-party library (e.g., MessageFormat.NET): additional dependency, unknown AOT status.

**Why chosen**: JSON files are easy for translators to edit, git-diff friendly, and the source-generated `JsonSerializerContext` makes deserialization fully AOT-compatible.

### Decision 2: `ILocalizer` resolved per-request, keyed by chat ID

**Chosen**: Register `ILocalizer` as a transient service. The `ChatLanguageRepository` provides the locale for a given chat ID. Handlers call `localizer.Get(chatId, MessageKey.BuyGroupOnly)`.

**Alternatives considered**:
- Pass locale string to handlers as a parameter from the dispatcher: couples dispatcher to i18n logic.
- Ambient/thread-local locale: not suitable for async concurrent message handling.

**Why chosen**: Keeps the handler interface clean; `ILocalizer` encapsulates both the locale lookup and the string resolution. The dispatcher doesn't need to change.

### Decision 3: Language preference stored in the existing `Groups` table

**Chosen**: Add a `LanguageCode TEXT NOT NULL DEFAULT 'ru'` column to the `Groups` table via `ALTER TABLE`. The `GroupRepository.GetOrCreateAsync` path already returns a `Group` per chat; we add `LanguageCode` to the model and repository.

**Alternatives considered**:
- Separate `ChatSettings` table: over-engineering for a single column.
- Flat file / in-memory override: doesn't survive restarts.

**Why chosen**: Minimal schema change; consistent with existing data access patterns.

### Decision 4: Migrate with `ALTER TABLE IF NOT EXISTS` in `DatabaseInitializer`

**Chosen**: Add a safe `ALTER TABLE Groups ADD COLUMN LanguageCode TEXT NOT NULL DEFAULT 'ru'` attempt inside `DatabaseInitializer.StartAsync`, wrapped in a try/catch that ignores the "duplicate column" error. This avoids tracking migration versions for a single-column addition.

**Alternatives considered**:
- Full migration framework (FluentMigrator, EF Migrations): heavyweight for an embedded SQLite project.
- `CREATE TABLE IF NOT EXISTS` replacement: destructive for existing data.

**Why chosen**: Simple, idempotent, production-safe. Rolls back trivially by ignoring the column.

## Risks / Trade-offs

- [Missing translation key at runtime] → `ILocalizer.Get` falls back to the English string and logs a warning; never throws. This avoids silent failures surfacing as exceptions.
- [Default locale is Russian (`ru`)] → Existing chats keep their current behavior after the migration adds `DEFAULT 'ru'`. No user-facing change unless `/language` is invoked.
- [JSON embedded resources increase binary size] → Negligible at this scale (two small files). AOT binary size impact is minimal.
- [ALTER TABLE approach is fragile for future multi-column migrations] → Acceptable now; the project should adopt a lightweight migration runner if a second schema change of this kind is needed.

## Migration Plan

1. `DatabaseInitializer` attempts `ALTER TABLE Groups ADD COLUMN LanguageCode TEXT NOT NULL DEFAULT 'ru'`; swallows `SqliteException` for duplicate column on already-migrated databases.
2. `Group` model gains `LanguageCode` property; `GroupRepository.GetOrCreateAsync` reads and writes it.
3. New `ChatLanguageRepository` (or extension on `GroupRepository`) provides `SetLanguageAsync(chatId, locale)`.
4. `Localizer` is registered in DI as `ILocalizer` (transient); loads embedded JSON at first use.
5. All handlers receive `ILocalizer` via constructor injection and switch from string literals to `localizer.Get(chatId, key)`.
6. `/language` command registered in `UpdateDispatcher`.

**Rollback**: Remove `/language` command handler, revert handlers to string literals, leave the `LanguageCode` column in place (ignored). No data loss.

## Open Questions

- Should group admins be the only ones allowed to change the chat language, or any member?
- Are Thai (`th`) or other locales in scope for the initial delivery, or only `en`/`ru`?
