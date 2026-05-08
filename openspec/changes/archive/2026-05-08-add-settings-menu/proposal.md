## Why

Users currently have no way to configure bot behavior or preferences. Adding a settings menu allows language selection and future preference customization, improving UX and localization support.

## What Changes

- New `/settings` command opens an interactive settings menu
- Users can select their preferred language (persisted to database)
- Extensible settings framework for future configuration options
- Settings are displayed and editable through inline buttons and dialogs

## Capabilities

### New Capabilities
- `user-settings`: Persistent user preference storage and retrieval, including language selection and access to settings UI

### Modified Capabilities
- `telegram-localization`: Settings menu text and UI elements must be localized; language preference selection is part of settings capability

## Impact

- New database table for user preferences with language setting
- New ICommandHandler (`/settings`) and ICallbackHandler for settings menu navigation
- New ILocalizer calls for settings UI text
- Settings state persisted to SQLite, queried during initialization and preference changes
- Optional UI extension: inline keyboard buttons for language options, dialog-based confirmation
