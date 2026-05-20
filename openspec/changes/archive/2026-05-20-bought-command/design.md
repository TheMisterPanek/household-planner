# Design: `/bought` command

## Context

The bot already has expiry tracking on the shopping-list "done" flow (`ShopDoneCallbackHandler` → `PriceCaptureStepHandler`, step 3). `ExpiryNotificationJob` fires daily and calls `ExpiryNotificationService.BuildSummaryAsync` which queries both `ShoppingItems` and `PurchaseHistory` for rows with a non-null `exp_date`.

Problem: No standalone command to register a bought item with an expiry date — users who buy items outside the shopping list have no way to track expiry.

## Goals / Non-Goals

**Goals:**
- Add `/bought` command with a 2-step dialog: item name (or inline) → expiry date → save to `PurchaseHistory`.
- Extract `ExpiryInputParser` static helper to eliminate duplication between `/bought` and the price-capture flow.

**Non-Goals:**
- Editing or deleting records entered via `/bought`.
- Price capture in the `/bought` flow (already handled by the shop-done path; `/bought` is expiry-only).
- Group-specific notification times.

## Decisions

### 1. `/bought` dialog: 2 steps, `BoughtDialogState`

**Decision**: New `BoughtDialogState` with `Step` (1 = item name, 2 = expiry date), `ItemName`, `Quantity`, `GroupId`, and `BoughtByName`.

**Step 1** is skipped if the user provides an inline argument (e.g. `/bought milk 2L`). The inline text is split by the first space to derive item name + optional quantity — same parsing convention as `BuyCommandHandler`.

**Step 2** reuses the same `ParseExpiryInput` logic already in `PriceCaptureStepHandler`. To avoid duplication, extract it to a static `ExpiryInputParser.Parse(string input)` helper.

**Finish**: writes a `PurchaseRecord` to `PurchaseHistoryRepository.AddAsync`. No price, no store — only `ItemName`, `Quantity`, `GroupId`, `UserId`, `PurchasedAt`, `BoughtByName`, and `ExpDate`. Then calls `IHistoryRepository.RecordAsync` (try/catch wrapped).

**Callback for skip (step 2):** `bought:skip_expiry` — 18 bytes, well within 64-byte limit. Handled by `BoughtSkipExpiryCallbackHandler`.

### 2. Group-only enforcement

`/bought` is a group-only command (expiry tracking is per-group). If invoked in a private chat, reply with the standard localized "group chat only" key and exit — same pattern as other group commands.

### 3. No schema changes

`PurchaseHistory` already has `exp_date TEXT NULL` from the prior migration. `/bought` records are plain rows with `Price = null` and `StoreName = null`. No new tables or columns required.

## Sequence Diagram — `/bought` flow

```
User                    Bot
 │                       │
 │  /bought milk 1L      │
 │──────────────────────▶│  BoughtCommandHandler
 │                       │    → parse inline args → ItemName="milk", Quantity="1L"
 │                       │    → GetOrCreateGroupAsync
 │                       │    → SetState(Step=2)
 │  "📅 Expiry for milk? [Skip]"
 │◀──────────────────────│
 │  "14 days"            │
 │──────────────────────▶│  BoughtStepHandler (Step 2)
 │                       │    → ExpiryInputParser.Parse("14 days") = today+14
 │                       │    → PurchaseHistoryRepository.AddAsync(record)
 │                       │    → IHistoryRepository.RecordAsync(...)
 │                       │    → ClearState
 │  "✓ milk registered, expires 03.06.2026"
 │◀──────────────────────│
```

```
User                    Bot
 │                       │
 │  /bought              │
 │──────────────────────▶│  BoughtCommandHandler
 │                       │    → no inline args → SetState(Step=1)
 │  "What did you buy?"  │
 │◀──────────────────────│
 │  "eggs 12"            │
 │──────────────────────▶│  BoughtStepHandler (Step 1)
 │                       │    → ItemName="eggs", Quantity="12"
 │                       │    → SetState(Step=2)
 │  "📅 Expiry for eggs? [Skip]"
 │◀──────────────────────│
 │  [Skip]               │
 │──────────────────────▶│  BoughtSkipExpiryCallbackHandler
 │                       │    → FinishDialogAsync(expDate=null)
 │  "✓ eggs registered"  │
 │◀──────────────────────│
```

## Files to Create / Modify

| File | Change |
|---|---|
| `Models/BoughtDialogState.cs` | New state model (Step, ItemName, Quantity, GroupId, BoughtByName) |
| `Handlers/ExpiryInputParser.cs` | New static helper extracted from `PriceCaptureStepHandler.ParseExpiryInput` |
| `Handlers/BoughtCommandHandler.cs` | New `ICommandHandler` for `/bought` |
| `Handlers/BoughtStepHandler.cs` | New `IDialogMessageHandler` for steps 1 and 2 |
| `Handlers/BoughtSkipExpiryCallbackHandler.cs` | New `ICallbackHandler` for `bought:skip_expiry` |
| `Handlers/PriceCaptureStepHandler.cs` | Delegate `ParseExpiryInput` to `ExpiryInputParser` |
| `Localization/Strings.en.json` | New keys: `bought.*` |
| `Localization/Strings.ru.json` | Same keys, Russian values |
| `Localization/Strings.pl.json` | Same keys, Polish values |
| `Program.cs` | Register `BoughtCommandHandler`, `BoughtStepHandler`, `BoughtSkipExpiryCallbackHandler`, `PendingDialogService<BoughtDialogState>` |

## AOT Compatibility

- No reflection or dynamic code generation.
- `BoughtDialogState` is a plain class — no JSON serialization needed (held in-memory).
- `ExpiryInputParser` uses only static string parsing.
