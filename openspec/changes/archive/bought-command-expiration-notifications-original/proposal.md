# Proposal: `/bought` command + Fix daily expiry notification summary

## What & Why

### Problem 1 — No quick way to register a bought item for expiry tracking

Users frequently buy items outside the bot's shopping-list flow (e.g., they pick something up without pre-planning). Currently, expiry tracking only triggers when an item is marked "done" from the shopping list. There is no standalone command to say "I just bought X, remind me it expires in 2 weeks."

The result: many bought items are never tracked, so the daily expiry summary is nearly empty and users miss expiry alerts.

### Problem 2 — Daily expiry notification does not send a useful summary

`ExpiryNotificationService.BuildSummaryAsync` has every section header and label hardcoded in Russian (e.g., `"📋 Сводка по срокам годности:"`, `"🔴 Просроченные:"`). Groups whose language is set to English or Polish receive a Russian-language message with no way to override it.

In addition `ExpiryNotificationJob` uses the older `System.Threading.Timer` callback pattern. If the callback throws before the async task is awaited, the exception is silently swallowed. The recommended pattern in this codebase is `IHostedService` + `PeriodicTimer`.

## Proposed Solution

### 1. `/bought` command — lightweight expiry registration

New `ICommandHandler` on command `/bought`.

**Usage:** `/bought` or `/bought <item name>`

**Dialog flow:**
```
/bought [optional inline name]
  → Step 1: "What did you buy?" (skipped if name given inline)
  → Step 2: "Expiry date for {item}? (e.g. 30, 1 week, 2 months)" [Skip]
  → finish: saves PurchaseRecord with ExpDate; confirms to user
```

The record is stored in `PurchaseHistory` (same table used by the shop-done flow), so `ExpiryNotificationService` picks it up automatically with no schema changes.

### 2. Fix `ExpiryNotificationService` — localise all strings

Replace every hardcoded Russian string in `BuildSummaryAsync` with `ILocalizer.Get(chatId, key)` calls, adding the required keys to `Strings.*.json` for en / ru / pl.

### 3. Fix `ExpiryNotificationJob` — switch to `PeriodicTimer`

Rewrite `ExpiryNotificationJob` to use `PeriodicTimer` instead of `Timer`. This matches the architecture rules and ensures exceptions surface correctly per iteration.

## Affected Components

| Component | Change |
|---|---|
| `BoughtCommandHandler` (new) | `/bought` command handler |
| `BoughtStepHandler` (new) | Dialog step handler for item name + expiry date |
| `BoughtDialogState` (new) | State model for the `/bought` dialog |
| `ExpiryNotificationService` | Replace hardcoded strings with `ILocalizer` calls |
| `ExpiryNotificationJob` | Rewrite with `PeriodicTimer` |
| `Strings.*.json` (en/ru/pl) | New keys for `/bought` prompts and notification headers |
| `Program.cs` | Register new handler and state service |

## Rollback Plan

All changes are additive:
- New handlers/state classes can be removed without touching existing code.
- New localization keys are harmless if unused.
- `PurchaseHistory` schema is unchanged.
- `ExpiryNotificationJob` rewrite is backward-compatible (same env var `NOTIFY_TIME_UTC`).

## Cross-Cutting Decisions

- Follows `ICommandHandler` + `IDialogMessageHandler` + `PendingDialogService<T>` pattern — no new dispatch mechanisms.
- `BoughtDialogState` uses the same `Step` integer model as `BuyDialogState` and `PriceCaptureDialogState`.
- All user-facing strings flow through `ILocalizer`; no hardcoded string literals in handlers.
- `PurchaseHistoryRepository.AddAsync` is reused unchanged.
- History audit: after successful save, call `IHistoryRepository.RecordAsync` (try/catch wrapped).
- `PeriodicTimer` is AOT-safe; no reflection or dynamic code generation introduced.
