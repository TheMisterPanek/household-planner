## Why

The bot currently sends all messages in a single hardcoded language, making it unusable for international groups or mixed-language households. Each Telegram chat should be able to configure its preferred language so that responses, prompts, and button labels are rendered in that language.

## What Changes

- Add a `chat-language` capability: a per-chat language preference stored and retrieved alongside other chat/group settings.
- Add a `localization` capability: a translation provider that resolves message strings by key and locale, used by all command/callback handlers.
- All user-facing bot messages (commands, list outputs, inline buttons, dialog prompts, error replies) will be rendered through the translation layer instead of hardcoded strings.
- A `/language` command lets users or admins select the chat language from a list of supported locales.

## Capabilities

### New Capabilities

- `chat-language`: Stores and retrieves a per-chat language preference (locale code, e.g. `en`, `th`, `ru`). Persisted in the existing SQLite database.
- `localization`: Provides translated message strings by key and locale. Backed by embedded resource files. Used by all handlers to produce user-facing text.

### Modified Capabilities

- `telegram-bot-core`: The update dispatcher and all handlers will now depend on a resolved locale per chat. Handlers receive localized strings instead of hardcoded literals.
- `shopping-list`: All user-facing messages in list display, buy flow prompts, and button labels will be routed through the localization capability.

## Impact

- **Database**: New column `language_code` on the `groups` table (or equivalent chat-settings record). Migration required.
- **Handlers**: All `ICommandHandler` and `ICallbackHandler` implementations updated to use `ILocalizer` instead of string literals.
- **New command**: `/language` command with inline keyboard for locale selection.
- **New files**: Translation resource files per supported locale (e.g., `Strings.en.json`, `Strings.th.json`).
- **Rollback**: Remove `/language` command registration and revert handlers to hardcoded strings. Database column can stay (nullable, ignored if feature absent).
- **Affected teams**: Bot feature development only — no external APIs affected.
- **AOT compatibility**: Translation resources must use source-generated JSON deserialization or embedded string dictionaries; no runtime reflection.
