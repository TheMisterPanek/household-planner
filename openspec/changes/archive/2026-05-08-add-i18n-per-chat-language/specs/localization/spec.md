## ADDED Requirements

### Requirement: ILocalizer resolves strings by chat ID and message key
The system SHALL provide an `ILocalizer` interface with a method `string Get(long chatId, string key)` that returns the localized string for the chat's current language. All user-facing bot messages SHALL be produced via `ILocalizer.Get` rather than hardcoded literals.

#### Scenario: Key resolved for known locale
- **WHEN** `Get` is called with a `chatId` whose `LanguageCode` is `"en"` and a key that exists in the English translation file
- **THEN** the English string for that key SHALL be returned

#### Scenario: Key falls back to English for unknown locale
- **WHEN** `Get` is called with a locale that has no translation file loaded
- **THEN** the English (`en`) string for the key SHALL be returned and a warning SHALL be logged

#### Scenario: Missing key falls back to key name
- **WHEN** `Get` is called with a key that does not exist in any loaded translation file
- **THEN** the key string itself SHALL be returned and a warning SHALL be logged

---

### Requirement: Translation data is loaded from embedded JSON files at startup
The system SHALL load translation dictionaries from embedded JSON resource files named `Strings.<locale>.json` (e.g., `Strings.en.json`, `Strings.ru.json`) at application startup using `System.Text.Json` with a source-generated `JsonSerializerContext`. The files SHALL be compiled into the assembly as embedded resources.

#### Scenario: Supported locale files are loaded successfully
- **WHEN** the application starts with valid embedded `Strings.en.json` and `Strings.ru.json` files
- **THEN** both dictionaries are available in memory and `ILocalizer.Get` can resolve keys for both locales without file I/O

#### Scenario: Missing embedded file does not crash startup
- **WHEN** a locale code is configured but no corresponding embedded file exists
- **THEN** the application logs a warning and falls back to English for that locale; it SHALL NOT throw on startup

---

### Requirement: Supported locales are discoverable at runtime
The system SHALL expose a static or injected list of supported locale codes and their native display names (e.g., `{ "en": "English", "ru": "Русский" }`) for use by the `/language` command handler.

#### Scenario: Locale list matches loaded files
- **WHEN** the supported locale list is queried
- **THEN** only locales whose `Strings.<locale>.json` file was successfully loaded at startup SHALL appear in the list

---

### Requirement: ILocalizer is AOT-compatible
The `ILocalizer` implementation and all translation loading code SHALL NOT use runtime reflection, `dynamic`, `Assembly.GetManifestResourceStream` with reflection-dependent paths, or any other pattern incompatible with .NET AOT compilation and trimming.

#### Scenario: AOT build succeeds with localization enabled
- **WHEN** the project is compiled with `PublishAot=true`
- **THEN** the build SHALL succeed without trimming warnings related to the localization code
