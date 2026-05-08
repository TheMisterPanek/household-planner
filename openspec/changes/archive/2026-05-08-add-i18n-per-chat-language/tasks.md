## 1. Database Migration

- [x] 1.1 Add `LanguageCode TEXT NOT NULL DEFAULT 'ru'` column to `Groups` table in `DatabaseInitializer.StartAsync`, wrapped in try/catch to ignore duplicate-column errors
- [x] 1.2 Add `LanguageCode` property to the `Group` model record
- [x] 1.3 Update `GroupRepository.GetOrCreateAsync` to read and write `LanguageCode` when inserting/selecting rows
- [x] 1.4 Add `SetLanguageAsync(long chatId, string languageCode)` method to `GroupRepository`

## 2. Localization Infrastructure

- [x] 2.1 Create `Strings.en.json` and `Strings.ru.json` embedded resource files covering all user-facing message keys (buy prompts, confirmations, list header, button labels, error messages, language-picker confirmation)
- [x] 2.2 Add a `[JsonSerializable(typeof(Dictionary<string, string>))]` source-generated `JsonSerializerContext` for AOT-safe deserialization of translation files
- [x] 2.3 Define `ILocalizer` interface with `string Get(long chatId, string key)` method
- [x] 2.4 Implement `Localizer` class that loads both embedded JSON files at startup and resolves strings by chat language (falls back to `en` for unknown locales; falls back to key name for missing keys, with a warning log)
- [x] 2.5 Register `ILocalizer` as a singleton in `Program.cs` DI container

## 3. Unit Tests — Localization

- [x] 3.1 Test `Localizer.Get` returns the correct string for a known locale and key
- [x] 3.2 Test `Localizer.Get` falls back to English for an unknown locale
- [x] 3.3 Test `Localizer.Get` returns the key name and logs a warning for a missing key
- [x] 3.4 Test `GroupRepository.SetLanguageAsync` persists the new language code and is readable via `GetOrCreateAsync`

## 4. /language Command

- [x] 4.1 Create `LanguageCommandHandler : ICommandHandler` for the `/language` command that sends an inline keyboard with one button per supported locale (native name as label, `lang:<code>` as callback data)
- [x] 4.2 Create `LanguageCallbackHandler : ICallbackHandler` with `CallbackPrefix = "lang:"` that calls `GroupRepository.SetLanguageAsync` and replies with a localized confirmation message
- [x] 4.3 Register both handlers in `Program.cs` DI

## 5. Update Existing Handlers to Use ILocalizer

- [x] 5.1 Inject `ILocalizer` into `BuyCommandHandler`; replace all hardcoded Russian strings with `localizer.Get(chatId, key)` calls
- [x] 5.2 Inject `ILocalizer` into `BuyStepHandler`; replace hardcoded "Сколько?" prompt and "Пропустить" button label
- [x] 5.3 Inject `ILocalizer` into `ShoppingListService`; replace "Список покупок пуст", list header, and "✗ Убрать" button label
- [x] 5.4 Inject `ILocalizer` into `ShopDoneCallbackHandler`; replace confirmation message template
- [x] 5.5 Inject `ILocalizer` into `BuySkipCallbackHandler`; replace confirmation message template

## 6. Unit Tests — Handler Behavior

- [x] 6.1 Test `BuyCommandHandler` uses localized "group-only" message when called from a private chat
- [x] 6.2 Test `BuyCommandHandler` uses localized "What to buy?" prompt when no inline args provided
- [x] 6.3 Test `LanguageCommandHandler` sends an inline keyboard containing buttons for each supported locale
- [x] 6.4 Test `LanguageCallbackHandler` calls `SetLanguageAsync` with the correct locale code and replies with a localized confirmation

## 7. Verify AOT Compatibility

- [x] 7.1 Run `dotnet publish -r linux-x64 -p:PublishAot=true` and confirm no trimming warnings are emitted from localization code
