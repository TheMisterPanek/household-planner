## Context

Currently, user preferences are not persisted. Language defaults to a system-wide setting. There's no settings menu for users to customize their experience. Adding settings requires a new database table, a command handler, callback handlers for menu navigation, and preference persistence.

## Goals / Non-Goals

**Goals:**
- Allow users to select and persist their language preference
- Provide extensible framework for future settings (timezone, notification preferences, etc.)
- Settings accessed via `/settings` command with inline keyboard UI
- Language preference loaded on chat initialization and used for all localizer calls

**Non-Goals:**
- Admin panel or global settings override
- Fine-grained permission controls over settings
- Settings sync across multiple chat contexts (per-chat settings only)

## Decisions

**Decision 1: Database Schema**
- New table `user_preferences` with columns: `chat_id` (PK), `language_code`, `created_at`, `updated_at`
- Initialized in `DatabaseInitializer.StartAsync` with standard `CREATE TABLE IF NOT EXISTS` pattern
- Simple flat schema; future settings added as new columns or a JSON `settings` column if complex

**Decision 2: Language Preference Lookup**
- Language preference resolved per-chat on every request using `IPreferenceRepository.GetLanguageAsync(chatId)`
- No caching; database lookup is fast on SQLite and keeps consistency simple
- Falls back to default locale if no preference stored

**Decision 3: Settings Command and Callback Routing**
- `/settings` command implemented as `ICommandHandler` that posts inline keyboard with language options
- Language selection implemented as `ICallbackHandler` with callback prefix `settings_lang`
- Callback data format: `settings_lang:<language_code>` (fits easily within 64-byte limit)
- After selection, update database and confirm with localized message

**Decision 4: Localization**
- All settings UI text (menu prompt, button labels, confirmation) resolved via `ILocalizer`
- Buttons display language names in both native and English for clarity (e.g., "English", "Русский (Russian)")

## Risks / Trade-offs

**[Risk] Settings menu could be abused by bots or spammers**
→ Mitigation: Telegram rate-limits user actions; no additional guards needed at this layer.

**[Risk] Language column may not be enough for future settings**
→ Mitigation: Acceptable; when settings grow complex, migrate to a JSON `settings` column via `ALTER TABLE`.

**[Risk] No transaction guarantee if preference update fails**
→ Mitigation: Non-critical; missing preference falls back to default locale. Log at Warning and continue.

## Migration Plan

1. Deploy database schema change (new `user_preferences` table) via `DatabaseInitializer`
2. Register `ICommandHandler` and `ICallbackHandler` implementations
3. Update `ILocalizer` to load language preference per-chat before returning keys
4. Test language switching in private chat with `/settings` command
5. Rollback: Drop `user_preferences` table if needed; command becomes unavailable but no data loss for other features

## Open Questions

- Should settings be per-group or per-user across groups? (Proposal: per-chat for simplicity)
- How many language options to support initially? (Spec will define)
- Should there be an admin setting to force a default language for a group?
