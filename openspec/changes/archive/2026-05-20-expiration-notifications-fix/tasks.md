## 1. Localization keys — `notify.*`

- [x] 1.1 Add to `Strings.en.json`:
  - `"notify.header"`: `"📋 Expiry summary:"`
  - `"notify.section-expired"`: `"🔴 Expired:"`
  - `"notify.section-today"`: `"🟡 Expires today:"`
  - `"notify.section-soon"`: `"🟠 Expires soon (≤3 days):"`
  - `"notify.section-week"`: `"📅 Expires this week (4–7 days):"`
- [x] 1.2 Add the same keys to `Strings.ru.json` with Russian values:
  - `"notify.header"`: `"📋 Сводка по срокам годности:"`
  - `"notify.section-expired"`: `"🔴 Просроченные:"`
  - `"notify.section-today"`: `"🟡 Истекает сегодня:"`
  - `"notify.section-soon"`: `"🟠 Истекает скоро (до 3 дней):"`
  - `"notify.section-week"`: `"📅 Истекает на этой неделе (4–7 дней):"`
- [x] 1.3 Add the same keys to `Strings.pl.json` with Polish values:
  - `"notify.header"`: `"📋 Podsumowanie dat ważności:"`
  - `"notify.section-expired"`: `"🔴 Przeterminowane:"`
  - `"notify.section-today"`: `"🟡 Wygasa dzisiaj:"`
  - `"notify.section-soon"`: `"🟠 Wygasa wkrótce (≤3 dni):"`
  - `"notify.section-week"`: `"📅 Wygasa w tym tygodniu (4–7 dni):"`

---

## 2. Fix `ExpiryNotificationService` — localization

- [x] 2.1 Replace the hardcoded `"📋 Сводка по срокам годности:"` with `localizer.Get(chatId, "notify.header")`.
- [x] 2.2 Replace `"🔴 Просроченные:"` with `localizer.Get(chatId, "notify.section-expired")`.
- [x] 2.3 Replace `"🟡 Истекает сегодня:"` with `localizer.Get(chatId, "notify.section-today")`.
- [x] 2.4 Replace `"🟠 Истекает скоро (до 3 дней):"` with `localizer.Get(chatId, "notify.section-soon")`.
- [x] 2.5 Replace `"📅 Истекает на этой неделе (4–7 дней):"` with `localizer.Get(chatId, "notify.section-week")`.

---

## 3. Fix `ExpiryNotificationJob` — switch to `PeriodicTimer`

- [x] 3.1 Remove the `Timer?` and `CancellationTokenSource?` fields.
- [x] 3.2 Add a `Task? _task` and `CancellationTokenSource? _cts` for the hosted service lifecycle.
- [x] 3.3 Implement `StartAsync`: create linked `_cts`, start `_task = RunAsync(_cts.Token)` (don't await).
- [x] 3.4 Implement private `async Task RunAsync(CancellationToken ct)`:
  - Compute initial delay to first UTC fire time (reuse existing `ParseNotifyTime`).
  - `await Task.Delay(initialDelay, ct)` — cancellable.
  - `using var timer = new PeriodicTimer(TimeSpan.FromHours(24))`.
  - Loop: `await ExecuteNotificationAsync(ct)`, then `await timer.WaitForNextTickAsync(ct)`.
  - Wrap the entire method body in `try/catch(OperationCanceledException)` — log at `Information` and return.
- [x] 3.5 Implement `StopAsync`: `_cts?.Cancel()`, then `await (_task ?? Task.CompletedTask)`.
- [x] 3.6 Remove the `TimerCallback` private method (no longer needed).
- [x] 3.7 Run `dotnet build` and confirm 0 errors.

---

## 4. Unit tests — `ExpiryNotificationService` (localization)

- [x] 4.1 `BuildSummaryAsync` with expired item → returned string contains `localizer` key value for `notify.section-expired`, not the hardcoded Russian text.
- [x] 4.2 `BuildSummaryAsync` with no expiry items → returns `null`.
- [x] 4.3 `BuildSummaryAsync` with items in all four buckets → all four section localizer keys are present in the output.

---

## 5. Smoke test (manual e2e — do NOT mark complete until confirmed by user)

1. Wait for 09:00 UTC (or temporarily override `NOTIFY_TIME_UTC` env var to a near-future time) → bot should send a daily summary in the group with properly localized headers (English if the group is set to English).
2. Switch group language to Russian (`/settings` → language → Russian) and verify the next summary uses Russian headers.
3. Switch to Polish and verify Polish headers.
4. Verify a group with no expiry-tracked items does not receive a notification.
