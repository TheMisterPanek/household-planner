## 1. Extract `ExpiryInputParser` static helper

- [ ] 1.1 Create `Handlers/ExpiryInputParser.cs` with a single public static method `Parse(string input)` returning `DateOnly?` — copy the logic from `PriceCaptureStepHandler.ParseExpiryInput` verbatim.
- [ ] 1.2 Update `PriceCaptureStepHandler` to delegate to `ExpiryInputParser.Parse` instead of calling the private method.
- [ ] 1.3 Verify `dotnet build` passes with 0 errors.

---

## 2. `BoughtDialogState` model

- [ ] 2.1 Create `Models/BoughtDialogState.cs`:
  - `Step` (int): 1 = waiting for item name, 2 = waiting for expiry date.
  - `ItemName` (string)
  - `Quantity` (string?)
  - `GroupId` (int)
  - `BoughtByName` (string, default `string.Empty`)

---

## 3. Localization keys

- [ ] 3.1 Add to `Strings.en.json`:
  - `"bought.what-did-you-buy"`: `"What did you buy? (e.g. milk 1L)"`
  - `"bought.expiry-prompt"`: `"📅 Expiry date for {item}? Enter days (e.g. 14) or a variant (e.g. 1 week, 2 months) or click Skip."`
  - `"bought.skip-expiry"`: `"Skip"`
  - `"bought.done"`: `"✓ {item} registered"`
  - `"bought.done-with-expiry"`: `"✓ {item} registered, expires {expiry}"`
  - `"notify.header"`: `"📋 Expiry summary:"`
  - `"notify.section-expired"`: `"🔴 Expired:"`
  - `"notify.section-today"`: `"🟡 Expires today:"`
  - `"notify.section-soon"`: `"🟠 Expires soon (≤3 days):"`
  - `"notify.section-week"`: `"📅 Expires this week (4–7 days):"`
- [ ] 3.2 Add the same keys to `Strings.ru.json` with Russian values:
  - `"bought.what-did-you-buy"`: `"Что вы купили? (например: молоко 1л)"`
  - `"bought.expiry-prompt"`: `"📅 Срок годности для {item}? Введите дни (например: 14) или вариант (1 неделя, 2 месяца) или нажмите Пропустить."`
  - `"bought.skip-expiry"`: `"Пропустить"`
  - `"bought.done"`: `"✓ {item} зарегистрировано"`
  - `"bought.done-with-expiry"`: `"✓ {item} зарегистрировано, срок до {expiry}"`
  - `"notify.header"`: `"📋 Сводка по срокам годности:"`
  - `"notify.section-expired"`: `"🔴 Просроченные:"`
  - `"notify.section-today"`: `"🟡 Истекает сегодня:"`
  - `"notify.section-soon"`: `"🟠 Истекает скоро (до 3 дней):"`
  - `"notify.section-week"`: `"📅 Истекает на этой неделе (4–7 дней):"`
- [ ] 3.3 Add the same keys to `Strings.pl.json` with Polish values:
  - `"bought.what-did-you-buy"`: `"Co kupiłeś? (np. mleko 1L)"`
  - `"bought.expiry-prompt"`: `"📅 Data ważności dla {item}? Podaj dni (np. 14) lub wariant (1 tydzień, 2 miesiące) lub kliknij Pomiń."`
  - `"bought.skip-expiry"`: `"Pomiń"`
  - `"bought.done"`: `"✓ {item} zarejestrowano"`
  - `"bought.done-with-expiry"`: `"✓ {item} zarejestrowano, wygasa {expiry}"`
  - `"notify.header"`: `"📋 Podsumowanie dat ważności:"`
  - `"notify.section-expired"`: `"🔴 Przeterminowane:"`
  - `"notify.section-today"`: `"🟡 Wygasa dzisiaj:"`
  - `"notify.section-soon"`: `"🟠 Wygasa wkrótce (≤3 dni):"`
  - `"notify.section-week"`: `"📅 Wygasa w tym tygodniu (4–7 dni):"`

---

## 4. `BoughtCommandHandler`

- [ ] 4.1 Create `Handlers/BoughtCommandHandler.cs` implementing `ICommandHandler`:
  - `Command = "/bought"`
  - If invoked in a private chat → reply with `localizer.Get(chatId, "common.group-only")` and return.
  - Call `groupRepository.GetOrCreateAsync(chatId)` to resolve `groupId`.
  - Parse inline argument: if message text has content after `/bought` (trimmed), split on first whitespace to get `itemName` and optional `quantity`.
  - If `itemName` is provided → set `Step = 2`, store `ItemName` and `Quantity`, reply with the expiry prompt (key `"bought.expiry-prompt"`) and a `[Skip]` inline button (`bought:skip_expiry`).
  - If no inline args → set `Step = 1`, reply with `"bought.what-did-you-buy"`.

---

## 5. `BoughtStepHandler`

- [ ] 5.1 Create `Handlers/BoughtStepHandler.cs` implementing `IDialogMessageHandler`:
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

- [ ] 6.1 Create `Handlers/BoughtSkipExpiryCallbackHandler.cs` implementing `ICallbackHandler`:
  - `CallbackPrefix = "bought"` (handles `bought:skip_expiry`).
  - On match: retrieve state; if null → answer callback and return.
  - Call `BoughtStepHandler.FinishDialogAsync` (or extract shared finish logic) with `expDate = null`.
  - Answer the callback query.

---

## 7. Fix `ExpiryNotificationService` — localization

- [ ] 7.1 Replace the hardcoded `"📋 Сводка по срокам годности:"` with `localizer.Get(chatId, "notify.header")`.
- [ ] 7.2 Replace `"🔴 Просроченные:"` with `localizer.Get(chatId, "notify.section-expired")`.
- [ ] 7.3 Replace `"🟡 Истекает сегодня:"` with `localizer.Get(chatId, "notify.section-today")`.
- [ ] 7.4 Replace `"🟠 Истекает скоро (до 3 дней):"` with `localizer.Get(chatId, "notify.section-soon")`.
- [ ] 7.5 Replace `"📅 Истекает на этой неделе (4–7 дней):"` with `localizer.Get(chatId, "notify.section-week")`.

---

## 8. Fix `ExpiryNotificationJob` — switch to `PeriodicTimer`

- [ ] 8.1 Remove the `Timer?` and `CancellationTokenSource?` fields.
- [ ] 8.2 Add a `Task? _task` and `CancellationTokenSource? _cts` for the hosted service lifecycle.
- [ ] 8.3 Implement `StartAsync`: create linked `_cts`, start `_task = RunAsync(_cts.Token)` (don't await).
- [ ] 8.4 Implement private `async Task RunAsync(CancellationToken ct)`:
  - Compute initial delay to first UTC fire time (reuse existing `ParseNotifyTime`).
  - `await Task.Delay(initialDelay, ct)` — cancellable.
  - `using var timer = new PeriodicTimer(TimeSpan.FromHours(24))`.
  - Loop: `await ExecuteNotificationAsync(ct)`, then `await timer.WaitForNextTickAsync(ct)`.
  - Wrap the entire method body in `try/catch(OperationCanceledException)` — log at `Information` and return.
- [ ] 8.5 Implement `StopAsync`: `_cts?.Cancel()`, then `await (_task ?? Task.CompletedTask)`.
- [ ] 8.6 Remove the `TimerCallback` private method (no longer needed).

---

## 9. DI Registration in `Program.cs`

- [ ] 9.1 Register `PendingDialogService<BoughtDialogState>` as `AddSingleton`.
- [ ] 9.2 Register `BoughtCommandHandler` as `AddScoped<ICommandHandler, BoughtCommandHandler>`.
- [ ] 9.3 Register `BoughtStepHandler` as `AddScoped<IDialogMessageHandler, BoughtStepHandler>`.
- [ ] 9.4 Register `BoughtSkipExpiryCallbackHandler` as `AddScoped<ICallbackHandler, BoughtSkipExpiryCallbackHandler>`.
- [ ] 9.5 Run `dotnet build` and confirm 0 errors.

---

## 10. Unit tests — `ExpiryInputParser`

- [ ] 10.1 `Parse("14")` → `today + 14 days`.
- [ ] 10.2 `Parse("2 weeks")` → `today + 14 days`.
- [ ] 10.3 `Parse("1 month")` → `today + 1 month`.
- [ ] 10.4 `Parse("2 недели")` → `today + 14 days`.
- [ ] 10.5 `Parse("abc")` → `null`.
- [ ] 10.6 `Parse("0")` → `null` (boundary: days must be > 0).
- [ ] 10.7 `Parse("400")` → `null` (boundary: days > 365).

---

## 11. Unit tests — `BoughtCommandHandler`

- [ ] 11.1 Private chat → sends "group-only" message; no dialog state set.
- [ ] 11.2 `/bought` (no args) → sends "what-did-you-buy" prompt; state Step = 1.
- [ ] 11.3 `/bought milk 1L` → sends expiry prompt with item name in text; state Step = 2, ItemName = "milk", Quantity = "1L".
- [ ] 11.4 `/bought eggs` (no quantity) → state Step = 2, ItemName = "eggs", Quantity = null.

---

## 12. Unit tests — `BoughtStepHandler`

- [ ] 12.1 Step 1 text → parses item name + quantity, advances to Step 2, sends expiry prompt.
- [ ] 12.2 Step 2 valid text → calls `PurchaseHistoryRepository.AddAsync`; state cleared; reply contains item name.
- [ ] 12.3 Step 2 with expiry → reply contains formatted date.
- [ ] 12.4 Step 2 invalid text → sends error message; state still Step 2.

---

## 13. Unit tests — `BoughtSkipExpiryCallbackHandler`

- [ ] 13.1 Skip with active state → `PurchaseHistoryRepository.AddAsync` called with `ExpDate = null`; state cleared.
- [ ] 13.2 Skip with no state → callback answered; no repository call.

---

## 14. Unit tests — `ExpiryNotificationService` (localization)

- [ ] 14.1 `BuildSummaryAsync` with expired item → returned string contains `localizer` key value for `notify.section-expired`, not the hardcoded Russian text.
- [ ] 14.2 `BuildSummaryAsync` with no expiry items → returns `null`.
- [ ] 14.3 `BuildSummaryAsync` with items in all four buckets → all four section localizer keys are present in the output.

---

## 15. Integration test — `/bought` flow

- [ ] 15.1 `/bought milk 1L` → bot sends expiry prompt; `PendingDialogService<BoughtDialogState>` has state with Step=2.
- [ ] 15.2 Text `"7 days"` after step 2 → `PurchaseHistory` table contains row with `ItemName="milk"`, `Quantity="1L"`, `exp_date` set to today+7; bot confirms.
- [ ] 15.3 `/bought eggs` then `[Skip]` callback → `PurchaseHistory` row with `exp_date = null`; state cleared.
- [ ] 15.4 `/bought` in private chat → bot sends group-only message; no state set.

---

## 16. Smoke test (manual e2e — do NOT mark complete until confirmed by user)

Send the following in a Telegram group chat where the bot is active:

1. `/bought milk 2L` → bot should ask for expiry date with a `[Skip]` button.
2. Type `7 days` → bot should confirm: "✓ milk registered, expires DD.MM.YYYY" (date should be today+7).
3. `/bought juice` → bot should ask "What did you buy?" (no, wait — `/bought juice` has inline arg, so it should skip step 1 and ask for expiry). Verify expiry prompt appears.
4. Tap `[Skip]` → bot should confirm: "✓ juice registered" (no expiry date).
5. `/bought` (no args) → bot should ask "What did you buy?".
6. Type `eggs 12` → bot should ask for expiry.
7. Type `abc` → bot should reply with invalid-format error (dialog stays open).
8. Type `1 month` → bot should confirm with a date ~30 days ahead.
9. Wait for 09:00 UTC (or temporarily override `NOTIFY_TIME_UTC` env var to a near-future time) → bot should send a daily summary in the group with properly localized headers (English if the group is set to English).
10. Switch group language to Russian (`/settings` → language → Russian) and verify the next morning summary uses Russian headers.
