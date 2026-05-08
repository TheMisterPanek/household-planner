## ADDED Requirements

### Requirement: User can open settings menu
The system SHALL provide a `/settings` command that displays an interactive settings menu with available configuration options.

#### Scenario: User opens settings menu
- **WHEN** a user sends `/settings` command in a private chat
- **THEN** the bot replies with a message listing available settings with inline buttons

#### Scenario: Settings command in group chat
- **WHEN** a user sends `/settings` command in a group chat
- **THEN** the bot replies with a localized "group chat only" message

### Requirement: User can select language preference
The system SHALL allow users to select their preferred language from a list of available languages.

#### Scenario: User selects language from menu
- **WHEN** user clicks a language button in the settings menu
- **THEN** the system saves the language preference to the database and confirms with a localized message in the selected language

#### Scenario: Language preference persists across sessions
- **WHEN** user selects a language and closes the chat
- **THEN** when the user reopens the chat, all bot messages use the previously selected language

#### Scenario: Language preference defaults to system default
- **WHEN** a new user who has never set a language preference receives a message
- **THEN** the bot uses the system default language

### Requirement: User preferences are persisted in database
The system SHALL store user preferences in a dedicated database table.

#### Scenario: Language preference stored in database
- **WHEN** user selects a language preference
- **THEN** the preference is saved to the `user_preferences` table with the user's chat ID and language code

#### Scenario: Language preference retrieved on startup
- **WHEN** the application starts and receives a message from a user
- **THEN** it queries the `user_preferences` table to load the user's language preference

### Requirement: Settings menu supports extensibility
The system SHALL be designed to allow additional settings options in the future without requiring major refactoring.

#### Scenario: Settings framework supports multiple options
- **WHEN** a developer adds a new setting option (e.g., timezone, notification preference)
- **THEN** they can extend the settings menu by adding a new button and handler without modifying existing language preference logic

### Requirement: All settings UI text is localized
The system SHALL resolve all user-facing settings text through the localization system.

#### Scenario: Settings menu buttons are localized
- **WHEN** user opens settings menu in different languages
- **THEN** all button labels and menu text appear in the user's preferred language
