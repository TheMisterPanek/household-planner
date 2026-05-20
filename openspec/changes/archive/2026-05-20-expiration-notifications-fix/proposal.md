# Proposal: Fix daily expiry notification summary

## What & Why

`ExpiryNotificationService.BuildSummaryAsync` has every section header and label hardcoded in Russian (e.g., `"📋 Сводка по срокам годности:"`, `"🔴 Просроченные:"`). Groups whose language is set to English or Polish receive a Russian-language message with no way to override it.

Additionally, `ExpiryNotificationJob` uses the older `System.Threading.Timer` callback pattern. If the callback throws before the async task is awaited, the exception is silently swallowed. The recommended pattern in this codebase is `IHostedService` + `PeriodicTimer`.

## Proposed Solution

### 1. Fix `ExpiryNotificationService` — localise all strings

Replace every hardcoded Russian string in `BuildSummaryAsync` with `ILocalizer.Get(chatId, key)` calls, adding the required keys to `Strings.*.json` for en / ru / pl.

### 2. Fix `ExpiryNotificationJob` — switch to `PeriodicTimer`

Rewrite `ExpiryNotificationJob` to use `PeriodicTimer` instead of `Timer`. This matches the architecture rules and ensures exceptions surface correctly per iteration.

## Affected Components

| Component | Change |
|---|---|
| `ExpiryNotificationService` | Replace hardcoded strings with `ILocalizer` calls |
| `ExpiryNotificationJob` | Rewrite with `PeriodicTimer` |
| `Strings.*.json` (en/ru/pl) | New `notify.*` localization keys |

## Rollback Plan

- `ExpiryNotificationJob` rewrite is backward-compatible (same env var `NOTIFY_TIME_UTC`).
- New localization keys are harmless if unused.
- `PurchaseHistory` schema is unchanged.

## Cross-Cutting Decisions

- `BuildSummaryAsync` already receives `chatId` so no signature change is needed.
- `PeriodicTimer` is AOT-safe; no reflection or dynamic code generation introduced.
