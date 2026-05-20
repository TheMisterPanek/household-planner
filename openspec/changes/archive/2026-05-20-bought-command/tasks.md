## 1. Extract `ExpiryInputParser` static helper

- [x] 1.1 Create `Handlers/ExpiryInputParser.cs` with a single public static method `Parse(string input)` returning `DateOnly?` — copy the logic from `PriceCaptureStepHandler.ParseExpiryInput` verbatim.
- [x] 1.2 Update `PriceCaptureStepHandler` to delegate to `ExpiryInputParser.Parse` instead of calling the private method.
- [x] 1.3 Verify `dotnet build` passes with 0 errors.

---

## 2. `BoughtDialogState` model

- [x] 2.1 Create `Models/BoughtDialogState.cs`:
  - `Step` (int): 1 = waiting for item name, 2 = waiting for expiry date.
  - `ItemName` (string)
  - `Quantity` (string?)
  - `GroupId` (int)
  - `BoughtByName` (string, default `string.Empty`)

---

## 3. Localization keys — `bought.*`

- [x] 3.1 Add to `Strings.en.json`:
  - `"bought.what-did-you-buy"`: `"What did you buy? (e.g. milk 1L)"`
  - `"bought.expiry-prompt"`: `"📅 Expiry date for {item}? Enter days (e.g. 14) or a variant (e.g. 1 week, 2 months) or click Skip."`
  - `"bought.skip-expiry"`: `"Skip"`
  - `"bought.done"`: `"✓ {item} registered"`
  - `"bought.done-with-expiry"`: `"✓ {item} registered, expires {expiry}"`
- [x] 3.2 Add the same keys to `Strings.ru.json` with Russian values:
  - `"bought.what-did-you-buy"`: `"Что вы купили? (например: молоко 1л)"`
  - `"bought.expiry-prompt"`: `"📅 Срок годности для {item}? Введите дни (например: 14) или вариант (1 неделя, 2 месяца) или нажмите Пропустить."`
  - `"bought.skip-expiry"`: `"Пропустить"`
  - `"bought.done"`: `"✓ {item} зарегистрировано"`
  - `"bought.done-with-expiry"`: `"✓ {item} зарегистрировано, срок до {expiry}"`
- [x] 3.3 Add the same keys to `Strings.pl.json` with Polish values:
  - `"bought.what-did-you-buy"`: `"Co kupiłeś? (np. mleko 1L)"`
  - `"bought.expiry-prompt"`: `"📅 Data ważności dla {item}? Podaj dni (np. 14) lub wariant (1 tydzień, 2 miesiące) lub kliknij Pomiń."`
  - `"bought.skip-expiry"`: `"Pomiń"`
  - `"bought.done"`: `"✓ {item} zarejestrowano"`
  - `"bought.done-with-expiry"`: `"✓ {item} zarejestrowano, wygasa {expiry}"`

---

## 4. `BoughtCommandHandler`

- [x] 4.1 Create `Handlers/BoughtCommandHandler.cs` implementing `ICommandHandler`:
  - `Command = "/bought"`
  - If invoked in a private chat → reply with `localizer.Get(chatId, "common.group-only")` and return.
  - Call `groupRepository.GetOrCreateAsync(chatId)` to resolve `groupId`.
  - Parse inline argument: if message text has content after `/bought` (trimmed), split on first whitespace to get `itemName` and optional `quantity`.
  - If `itemName` is provided → set `Step = 2`, store `ItemName` and `Quantity`, reply with the expiry prompt (key `"bought.expiry-prompt"`) and a `[Skip]` inline button (`bought:skip_expiry`).
  - If no inline args → set `Step = 1`, reply with `"bought.what-did-you-buy"`.

---

## 5. `BoughtStepHandler`

- [x] 5.1 Create `Handlers/BoughtStepHandler.cs` implementing `IDialogMessageHandler`:
  - `CanHandle`: returns `true` if `PendingDialogService<BoughtDialogState>.GetState(chatId, userId)` is not null.
  - **Step 1**: parse the user's text as `itemName [quantity]` (split on first space). Update state to `Step = 2`, `ItemName`, `Quantity`. Reply with expiry prompt + `[Skip]` button (`bought:skip_expiry`).
  - **Step 2**: call `ExpiryInputParser.Parse(input)`. If null → reply with `"shop.invalid-date"` and keep step open. If valid → call `FinishDialogAsync(expDate)`.
  - `FinishDialogAsync(expDate)`:
    - Build `PurchaseRecord` with `GroupId`, `UserId`, `ItemName`, `Quantity`, `PurchasedAt = DateTime.UtcNow`, `BoughtByName`, `ExpDate = expDate`, `Price = null`, `StoreName = null`.
    - `await purchaseRepository.AddAsync(record)`.
    - `try { await historyRepository.RecordAsync(...); } catch { logger.LogWarning(...); }`.
    - `dialogService.ClearState(chatId, userId)`.
    - Reply: if `expDate` is not null → `localizer.Get(chatId, "bought.done-with-expiry").Replace("{item}", ...).Replace("{expiry}", expDate.Value.ToString("dd.MM.yyyy"))`; else → `localizer.Get(chatId, "bought.done").Replace("{item}", ...)`.

---

## 6. `BoughtSkipExpiryCallbackHandler`

- [x] 6.1 Create `Handlers/BoughtSkipExpiryCallbackHandler.cs` implementing `ICallbackHandler`:
  - `CallbackPrefix = "bought"` (handles `bought:skip_expiry`).
  - On match: retrieve state; if null → answer callback and return.
  - Call `BoughtStepHandler.FinishDialogAsync` (or extract shared finish logic) with `expDate = null`.
  - Answer the callback query.

---

## 7. DI Registration in `Program.cs`

- [x] 7.1 Register `PendingDialogService<BoughtDialogState>` as `AddSingleton`.
- [x] 7.2 Register `BoughtCommandHandler` as `AddScoped<ICommandHandler, BoughtCommandHandler>`.
- [x] 7.3 Register `BoughtStepHandler` as `AddScoped<IDialogMessageHandler, BoughtStepHandler>`.
- [x] 7.4 Register `BoughtSkipExpiryCallbackHandler` as `AddScoped<ICallbackHandler, BoughtSkipExpiryCallbackHandler>`.
- [x] 7.5 Run `dotnet build` and confirm 0 errors.

---

## 8. Unit tests — `ExpiryInputParser`

- [x] 8.1 `Parse("14")` → `today + 14 days`.
- [x] 8.2 `Parse("2 weeks")` → `today + 14 days`.
- [x] 8.3 `Parse("1 month")` → `today + 1 month`.
- [x] 8.4 `Parse("2 недели")` → `today + 14 days`.
- [x] 8.5 `Parse("abc")` → `null`.
- [x] 8.6 `Parse("0")` → `null` (boundary: days must be > 0).
- [x] 8.7 `Parse("400")` → `null` (boundary: days > 365).

---

## 9. Unit tests — `BoughtCommandHandler`

- [x] 9.1 Private chat → sends "group-only" message; no dialog state set.
- [x] 9.2 `/bought` (no args) → sends "what-did-you-buy" prompt; state Step = 1.
- [x] 9.3 `/bought milk 1L` → sends expiry prompt with item name in text; state Step = 2, ItemName = "milk", Quantity = "1L".
- [x] 9.4 `/bought eggs` (no quantity) → state Step = 2, ItemName = "eggs", Quantity = null.

---

## 10. Unit tests — `BoughtStepHandler`

- [x] 10.1 Step 1 text → parses item name + quantity, advances to Step 2, sends expiry prompt.
- [x] 10.2 Step 2 valid text → calls `PurchaseHistoryRepository.AddAsync`; state cleared; reply contains item name.
- [x] 10.3 Step 2 with expiry → reply contains formatted date.
- [x] 10.4 Step 2 invalid text → sends error message; state still Step 2.

---

## 11. Unit tests — `BoughtSkipExpiryCallbackHandler`

- [x] 11.1 Skip with active state → `PurchaseHistoryRepository.AddAsync` called with `ExpDate = null`; state cleared.
- [x] 11.2 Skip with no state → callback answered; no repository call.

---

## 12. Integration test — `/bought` flow

- [x] 12.1 `/bought milk 1L` → bot sends expiry prompt; `PendingDialogService<BoughtDialogState>` has state with Step=2.
- [x] 12.2 Text `"7 days"` after step 2 → `PurchaseHistory` table contains row with `ItemName="milk"`, `Quantity="1L"`, `exp_date` set to today+7; bot confirms.
- [x] 12.3 `/bought eggs` then `[Skip]` callback → `PurchaseHistory` row with `exp_date = null`; state cleared.
- [x] 12.4 `/bought` in private chat → bot sends group-only message; no state set.

---

## 13. Smoke test (manual e2e — do NOT mark complete until confirmed by user)

Send the following in a Telegram group chat where the bot is active:

1. `/bought milk 2L` → bot should ask for expiry date with a `[Skip]` button.
2. Type `7 days` → bot should confirm: "✓ milk registered, expires DD.MM.YYYY" (date should be today+7).
3. `/bought juice` → bot should skip step 1 and ask for expiry. Verify expiry prompt appears.
4. Tap `[Skip]` → bot should confirm: "✓ juice registered" (no expiry date).
5. `/bought` (no args) → bot should ask "What did you buy?".
6. Type `eggs 12` → bot should ask for expiry.
7. Type `abc` → bot should reply with invalid-format error (dialog stays open).
8. Type `1 month` → bot should confirm with a date ~30 days ahead.
9. Try `/bought` in a private chat → bot should reply with "group chat only" message.
