## 1. Database Migration

- [ ] 1.1 Add `LanguageCode TEXT NOT NULL DEFAULT 'ru'` column to `Groups` table in `DatabaseInitializer.StartAsync`, wrapped in try/catch to ignore duplicate-column errors
- [ ] 1.2 Add `LanguageCode` property to the `Group` model record
- [ ] 1.3 Update `GroupRepository.GetOrCreateAsync` to read and write `LanguageCode` when inserting/selecting rows
- [ ] 1.4 Add `SetLanguageAsync(long chatId, string languageCode)` method to `GroupRepository`

## 2. Localization Infrastructure

- [ ] 2.1 Create `Strings.en.json` and `Strings.ru.json` embedded resource files covering all user-facing message keys (buy prompts, confirmations, list header, button labels, error messages, language-picker confirmation)
- [ ] 2.2 Add a `[JsonSerializable(typeof(Dictionary<string, string>))]` source-generated `JsonSerializerContext` for AOT-safe deserialization of translation files
- [ ] 2.3 Define `ILocalizer` interface with `string Get(long chatId, string key)` method
- [ ] 2.4 Implement `Localizer` class that loads both embedded JSON files at startup and resolves strings by chat language (falls back to `en` for unknown locales; falls back to key name for missing keys, with a warning log)
- [ ] 2.5 Register `ILocalizer` as a singleton in `Program.cs` DI container

## 3. Unit Tests — Localization

- [ ] 3.1 Test `Localizer.Get` returns the correct string for a known locale and key
- [ ] 3.2 Test `Localizer.Get` falls back to English for an unknown locale
- [ ] 3.3 Test `Localizer.Get` returns the key name and logs a warning for a missing key
- [ ] 3.4 Test `GroupRepository.SetLanguageAsync` persists the new language code and is readable via `GetOrCreateAsync`

## 4. /language Command

- [ ] 4.1 Create `LanguageCommandHandler : ICommandHandler` for the `/language` command that sends an inline keyboard with one button per supported locale (native name as label, `lang:<code>` as callback data)
- [ ] 4.2 Create `LanguageCallbackHandler : ICallbackHandler` with `CallbackPrefix = "lang:"` that calls `GroupRepository.SetLanguageAsync` and replies with a localized confirmation message
- [ ] 4.3 Register both handlers in `Program.cs` DI

## 5. Update Existing Handlers to Use ILocalizer

- [ ] 5.1 Inject `ILocalizer` into `BuyCommandHandler`; replace all hardcoded Russian strings with `localizer.Get(chatId, key)` calls
- [ ] 5.2 Inject `ILocalizer` into `BuyStepHandler`; replace hardcoded "Сколько?" prompt and "Пропустить" button label
- [ ] 5.3 Inject `ILocalizer` into `ShoppingListService`; replace "Список покупок пуст", list header, and "✗ Убрать" button label
- [ ] 5.4 Inject `ILocalizer` into `ShopDoneCallbackHandler`; replace confirmation message template
- [ ] 5.5 Inject `ILocalizer` into `BuySkipCallbackHandler`; replace confirmation message template

## 6. Unit Tests — Handler Behavior

- [ ] 6.1 Test `BuyCommandHandler` uses localized "group-only" message when called from a private chat
- [ ] 6.2 Test `BuyCommandHandler` uses localized "What to buy?" prompt when no inline args provided
- [ ] 6.3 Test `LanguageCommandHandler` sends an inline keyboard containing buttons for each supported locale
- [ ] 6.4 Test `LanguageCallbackHandler` calls `SetLanguageAsync` with the correct locale code and replies with a localized confirmation

## 7. Verify AOT Compatibility

- [ ] 7.1 Run `dotnet publish -r linux-x64 -p:PublishAot=true` and confirm no trimming warnings are emitted from localization code
