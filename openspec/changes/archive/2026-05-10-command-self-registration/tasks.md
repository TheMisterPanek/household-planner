## 1. Extend ICommandHandler interface

- [x] 1.1 Add `string? Description { get; }` to `ICommandHandler` with a default interface implementation returning `null`
- [x] 1.2 Verify all existing handler implementations compile without changes (default returns `null`)

## 2. Add Description to public command handlers

- [x] 2.1 Add `Description` to `BuyCommandHandler` (e.g. "Start shopping session")
- [x] 2.2 Add `Description` to `ListCommandHandler` (e.g. "View shopping list")
- [x] 2.3 Add `Description` to `MealsCommandHandler` (e.g. "Manage meal plans")
- [x] 2.4 Add `Description` to `PricesCommandHandler` (e.g. "View price history")
- [x] 2.5 Add `Description` to `SearchCommandHandler` (e.g. "Search items")
- [x] 2.6 Add `Description` to `HistoryCommandHandler` (e.g. "View action history")
- [x] 2.7 Add `Description` to `LanguageCommandHandler` (e.g. "Change bot language")
- [x] 2.8 Add `Description` to `SettingsCommandHandler` (e.g. "Bot settings")

## 3. BotCommandRegistrationService

- [x] 3.1 Create `BotCommandRegistrationService : IHostedService` in `ProductTrackerBot/`
- [x] 3.2 Inject `ITelegramBotClient` and `IEnumerable<ICommandHandler>`; on `StartAsync`, filter handlers by non-null `Description`, build `BotCommand[]`, call `SetMyCommandsAsync`
- [x] 3.3 Wrap `SetMyCommandsAsync` in try/catch; log Warning on failure; never throw from `StartAsync`
- [x] 3.4 Skip `SetMyCommandsAsync` entirely when no handler has a description
- [x] 3.5 Register `BotCommandRegistrationService` in `Program.cs` **before** `BotHostedService`

## 4. StartCommandHandler

- [x] 4.1 Create `Handlers/StartCommandHandler.cs` implementing `ICommandHandler` with `Command = "/start"` and `Description = null` (internal helper, not listed in suggestions)
- [x] 4.2 Inject `IEnumerable<ICommandHandler>`, `ITelegramBotClient`, `ILocalizer`
- [x] 4.3 Build reply: localized welcome header + one line per handler with non-null `Description` formatted as `/<command> — <description>`
- [x] 4.4 Add localization keys `start.welcome` and `start.no-commands` to `Strings.en.json`, `Strings.ru.json`, `Strings.pl.json`
- [x] 4.5 Register `StartCommandHandler` in `Program.cs`

## 5. Tests — BotCommandRegistrationService

- [x] 5.1 Test: calls `SetMyCommandsAsync` with correct list when handlers have descriptions
- [x] 5.2 Test: skips `SetMyCommandsAsync` when no handler has a description
- [x] 5.3 Test: logs Warning and does not throw when `SetMyCommandsAsync` throws

## 6. Tests — StartCommandHandler

- [x] 6.1 Test: replies with welcome message and command list when handlers have descriptions
- [x] 6.2 Test: replies with `start.no-commands` message when no handler has a description
- [x] 6.3 Test: `/start` handler itself has `Description = null` (excluded from its own list)

## 7. Build verification

- [x] 7.1 Run `dotnet build` — 0 errors, no new warnings
- [x] 7.2 Run `make test` — all tests pass
