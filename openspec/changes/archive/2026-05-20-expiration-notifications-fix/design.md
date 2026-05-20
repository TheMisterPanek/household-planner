# Design: Fix daily expiry notification summary

## Context

`ExpiryNotificationJob` fires daily and calls `ExpiryNotificationService.BuildSummaryAsync` which queries both `ShoppingItems` and `PurchaseHistory` for rows with a non-null `exp_date`. Two issues exist:

1. `BuildSummaryAsync` has every section header hardcoded in Russian — groups using English or Polish receive unlocalized output.
2. `ExpiryNotificationJob` uses `System.Threading.Timer` where exceptions thrown inside the async callback are silently swallowed.

## Goals / Non-Goals

**Goals:**
- Replace all hardcoded strings in `ExpiryNotificationService` with `ILocalizer` calls.
- Rewrite `ExpiryNotificationJob` using `PeriodicTimer`.

**Non-Goals:**
- Group-specific notification times.
- Any changes to the notification schedule or eligibility logic.

## Decisions

### 1. Localize `ExpiryNotificationService`

Replace the five hardcoded Russian strings in `BuildSummaryAsync` with `localizer.Get(chatId, key)` where `chatId` is the group's chat ID (already passed in). New keys:

| Key | en | ru | pl |
|---|---|---|---|
| `notify.header` | `📋 Expiry summary:` | `📋 Сводка по срокам годности:` | `📋 Podsumowanie dat ważności:` |
| `notify.section-expired` | `🔴 Expired:` | `🔴 Просроченные:` | `🔴 Przeterminowane:` |
| `notify.section-today` | `🟡 Expires today:` | `🟡 Истекает сегодня:` | `🟡 Wygasa dzisiaj:` |
| `notify.section-soon` | `🟠 Expires soon (≤3 days):` | `🟠 Истекает скоро (до 3 дней):` | `🟠 Wygasa wkrótce (≤3 dni):` |
| `notify.section-week` | `📅 Expires this week (4–7 days):` | `📅 Истекает на этой неделе (4–7 дней):` | `📅 Wygasa w tym tygodniu (4–7 dni):` |

`BuildSummaryAsync` already receives `chatId` so no signature change is needed.

### 2. Rewrite `ExpiryNotificationJob` with `PeriodicTimer`

Replace the `Timer` field with a `PeriodicTimer`. The hosted service pattern:

```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    _task = RunAsync(_cts.Token);
}

private async Task RunAsync(CancellationToken ct)
{
    await Task.Delay(InitialDelay(), ct);   // wait until first fire time
    using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
    do {
        await ExecuteNotificationAsync(ct);
    } while (await timer.WaitForNextTickAsync(ct));
}

public async Task StopAsync(CancellationToken cancellationToken)
{
    _cts?.Cancel();
    if (_task is not null) await _task.ConfigureAwait(false);
}
```

`PeriodicTimer` is AOT-safe, avoids callback re-entrancy, and propagates exceptions to the loop instead of silently swallowing them.

## Files to Modify

| File | Change |
|---|---|
| `Services/ExpiryNotificationService.cs` | Replace hardcoded strings with `ILocalizer` calls |
| `Services/ExpiryNotificationJob.cs` | Rewrite with `PeriodicTimer` |
| `Localization/Strings.en.json` | New keys: `notify.*` |
| `Localization/Strings.ru.json` | Same keys, Russian values |
| `Localization/Strings.pl.json` | Same keys, Polish values |

## AOT Compatibility

- `PeriodicTimer` is part of the BCL and trim-safe.
- No reflection or dynamic code generation.
