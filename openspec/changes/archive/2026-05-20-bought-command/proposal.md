# Proposal: `/bought` command — lightweight expiry registration

## What & Why

Users frequently buy items outside the bot's shopping-list flow (e.g., they pick something up without pre-planning). Currently, expiry tracking only triggers when an item is marked "done" from the shopping list. There is no standalone command to say "I just bought X, remind me it expires in 2 weeks."

The result: many bought items are never tracked, so the daily expiry summary is nearly empty and users miss expiry alerts.

## Proposed Solution

New `ICommandHandler` on command `/bought`.

**Usage:** `/bought` or `/bought <item name> [quantity]`

**Dialog flow:**
```
/bought [optional inline name]
  → Step 1: "What did you buy?" (skipped if name given inline)
  → Step 2: "Expiry date for {item}? (e.g. 30, 1 week, 2 months)" [Skip]
  → finish: saves PurchaseRecord with ExpDate; confirms to user
```

The record is stored in `PurchaseHistory` (same table used by the shop-done flow), so `ExpiryNotificationService` picks it up automatically with no schema changes.

An `ExpiryInputParser` static helper is extracted from `PriceCaptureStepHandler.ParseExpiryInput` to avoid duplication between the two flows.

## Affected Components

| Component | Change |
|---|---|
| `ExpiryInputParser` (new) | Static helper extracted from `PriceCaptureStepHandler` |
| `BoughtCommandHandler` (new) | `/bought` command handler |
| `BoughtStepHandler` (new) | Dialog step handler for item name + expiry date |
| `BoughtSkipExpiryCallbackHandler` (new) | `bought:skip_expiry` callback handler |
| `BoughtDialogState` (new) | State model for the `/bought` dialog |
| `PriceCaptureStepHandler` | Delegate to `ExpiryInputParser` |
| `Strings.*.json` (en/ru/pl) | New `bought.*` localization keys |
| `Program.cs` | Register new handlers and state service |

## Rollback Plan

All changes are additive — new handlers and state classes can be removed without touching existing code. `PurchaseHistory` schema is unchanged.

## Cross-Cutting Decisions

- Follows `ICommandHandler` + `IDialogMessageHandler` + `PendingDialogService<T>` pattern — no new dispatch mechanisms.
- `BoughtDialogState` mirrors the `Step` integer model used by `BuyDialogState` and `PriceCaptureDialogState`.
- All user-facing strings flow through `ILocalizer`.
- `PurchaseHistoryRepository.AddAsync` is reused unchanged.
- History audit: after successful save, call `IHistoryRepository.RecordAsync` (try/catch wrapped).
