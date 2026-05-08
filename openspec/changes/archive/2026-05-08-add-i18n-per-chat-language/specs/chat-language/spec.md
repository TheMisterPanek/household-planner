## ADDED Requirements

### Requirement: Chat language preference is persisted per chat
The system SHALL store a language code (e.g., `en`, `ru`) for each registered Telegram chat in the `Groups` table as a `LanguageCode` column with a default value of `ru`. The language code is included in the `Group` model returned by `GroupRepository.GetOrCreateAsync`.

#### Scenario: New chat has default language
- **WHEN** a chat sends its first command and `GetOrCreateAsync` creates a new `Groups` row
- **THEN** the returned `Group.LanguageCode` SHALL equal `"ru"`

#### Scenario: Existing chat language is read correctly
- **WHEN** `GetOrCreateAsync` is called for a chat that already has a `LanguageCode` stored
- **THEN** the returned `Group.LanguageCode` SHALL equal the stored value

---

### Requirement: Chat language can be updated
The system SHALL provide a `SetLanguageAsync(chatId, languageCode)` method on `GroupRepository` (or a dedicated `ChatLanguageRepository`) that updates the `LanguageCode` column for the given chat.

#### Scenario: Language update is persisted
- **WHEN** `SetLanguageAsync` is called with a valid locale code for an existing chat
- **THEN** the subsequent `GetOrCreateAsync` for the same `chatId` SHALL return `Group.LanguageCode` equal to the new locale code

#### Scenario: Setting language for unknown chat creates group first
- **WHEN** `SetLanguageAsync` is called for a `chatId` not yet in the database
- **THEN** the system SHALL create the `Groups` row with the specified language code and no `ListMessageId`

---

### Requirement: Database migration adds LanguageCode column safely
The system SHALL add `LanguageCode TEXT NOT NULL DEFAULT 'ru'` to the `Groups` table during startup via `DatabaseInitializer`, ignoring the error if the column already exists.

#### Scenario: First migration on a fresh database
- **WHEN** the application starts and the `Groups` table has no `LanguageCode` column
- **THEN** the column is added and all existing rows receive `LanguageCode = 'ru'`

#### Scenario: Idempotent migration on already-migrated database
- **WHEN** the application starts and `LanguageCode` already exists on `Groups`
- **THEN** `DatabaseInitializer` completes without error and existing language codes are unchanged

---

### Requirement: /language command lets users select the chat language
The system SHALL register a `/language` command handler that sends an inline keyboard listing all supported locales. When a locale button is pressed, the chat's language SHALL be updated via `SetLanguageAsync` and a confirmation message sent.

#### Scenario: /language shows locale picker
- **WHEN** a user sends `/language` in a group or private chat
- **THEN** the bot SHALL reply with an inline keyboard where each button label is the locale's native name and the callback data encodes the locale code

#### Scenario: User selects a language
- **WHEN** the user presses a locale button in the language picker
- **THEN** the bot SHALL call `SetLanguageAsync` with the chosen locale code and reply with a confirmation message in the newly selected language

#### Scenario: /language in unsupported context
- **WHEN** `/language` is sent in a channel (not a group or private chat)
- **THEN** the system SHALL silently ignore the command
