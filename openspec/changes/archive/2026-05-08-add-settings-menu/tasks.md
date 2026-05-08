## 1. Database Schema and Repository

- [x] 1.1 Add user_preferences table initialization in DatabaseInitializer.StartAsync
- [x] 1.2 Create IPreferenceRepository interface with GetLanguageAsync and SaveLanguageAsync methods
- [x] 1.3 Implement PreferenceRepository using raw Microsoft.Data.Sqlite
- [x] 1.4 Register IPreferenceRepository in dependency injection container
- [x] 1.5 Unit test: PreferenceRepository saves and retrieves language preference

## 2. Localization Keys

- [x] 2.1 Add localization keys for settings menu (menu_prompt, button_language, button_back, etc.)
- [x] 2.2 Add language option labels for all supported languages (English, Russian, etc.)
- [x] 2.3 Add confirmation message keys (language_saved, etc.)

## 3. Settings Command Handler

- [x] 3.1 Create SettingsCommandHandler implementing ICommandHandler with command "/settings"
- [x] 3.2 Allow settings in both private and group chats, stored per chat ID
- [x] 3.3 Build inline keyboard with language selection buttons
- [x] 3.4 Register SettingsCommandHandler in dependency injection
- [x] 3.5 Unit test: SettingsCommandHandler builds correct keyboard and response

## 4. Language Selection Callback Handler

- [x] 4.1 Create LanguageSelectionHandler implementing ICallbackHandler with callback prefix "settings_lang"
- [x] 4.2 Parse callback data to extract selected language code
- [x] 4.3 Call IPreferenceRepository.SaveLanguageAsync to persist selection
- [x] 4.4 Record action history via IHistoryRepository.RecordAsync
- [x] 4.5 Reply with confirmation message localized in selected language
- [x] 4.6 Register LanguageSelectionHandler in dependency injection
- [x] 4.7 Unit test: LanguageSelectionHandler parses callback data and saves preference
- [x] 4.8 Unit test: Confirmation message is localized in selected language

## 5. Localization Integration

- [x] 5.1 Modify ILocalizer to load user's language preference on first call per chat
- [x] 5.2 Cache language preference per chat session to avoid repeated database queries
- [x] 5.3 Fall back to default locale if preference is not found
- [x] 5.4 Unit test: ILocalizer returns strings in correct language based on saved preference

## 6. Integration and Manual Testing

- [x] 6.1 Run full test suite and verify no regressions
- [ ] 6.2 Manual test: Send /settings command in private and group chats
- [ ] 6.3 Manual test: Click language button and verify preference is saved per chat
- [ ] 6.4 Manual test: Verify language preference persists across bot restarts
- [ ] 6.5 Manual test: Verify different chats can have different language preferences
