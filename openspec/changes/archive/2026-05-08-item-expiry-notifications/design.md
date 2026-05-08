## Context

The bot stores shopping items in SQLite via `ShoppingItemRepository`. The `/buy` dialog currently has two steps: name (step 1) and quantity (step 2). Both `BuyStepHandler` (text entry) and `BuySkipCallbackHandler` (quantity skip) call `itemRepository.AddAsync` as a terminal action. There is no concept of item expiry and no background scheduling infrastructure.

Groups are registered lazily via `GroupRepository.GetOrCreateAsync`. Their `ChatId` values in the `Groups` table are the only handle for reaching all registered chats.

## Goals / Non-Goals

**Goals:**
- Add optional `ExpDate` (`DateOnly?`) to `ShoppingItem` — stored, persisted, and displayed.
- Extend `/buy` dialog with a step 3 (expiry date) after quantity, skippable.
- Daily notification job: sends one summary per group whose items have expiry data worth reporting.
- `/list` shows expiry date next to items that have one.

**Non-Goals:**
- Editing expiry date after item creation.
- Per-user notification preferences or time-zone personalisation.
- Push notifications outside Telegram (email, etc.).
- Automatic removal of expired items.

## Decisions

### 1. ExpDate column: TEXT (ISO-8601) via ALTER TABLE migration

SQLite has no `ADD COLUMN IF NOT EXISTS` syntax. `DatabaseInitializer` will query `PRAGMA table_info(ShoppingItems)` on startup and run `ALTER TABLE ShoppingItems ADD COLUMN exp_date TEXT` only if the column is absent. Existing rows get NULL — no data loss. TEXT with `yyyy-MM-dd` format is sortable lexicographically, keeps AOT-safe (no `System.Text.Json` custom converters needed for storage), and round-trips cleanly through `DateOnly.ParseExact`.

*Alternative considered*: INTEGER (Julian Day). Rejected — harder to read/debug in raw SQLite and requires a non-obvious mapping.

### 2. Extend /buy dialog to three steps; refactor skip-quantity path

`BuyDialogState.Step` gains a third state: `3 = waiting for expiry date`. Both the enter-quantity path (`BuyStepHandler.HandleStep2Async`) and the skip-quantity path (`BuySkipCallbackHandler`) must transition to step 3 instead of immediately persisting.

`BuySkipCallbackHandler` currently calls `itemRepository.AddAsync` directly. It will instead save `null` quantity to `state.Quantity`, advance `state.Step = 3`, and prompt "Срок годности? (например, 15.05.2026)".

A new `buy:skip_expiry` callback handler (`BuySkipExpiryCallbackHandler`) terminates the dialog when the user skips expiry.

Date input is accepted in `dd.MM.yyyy` format (Russian locale convention). Invalid input triggers an error reply and keeps the dialog open at step 3.

### 3. Daily notification: PeriodicTimer-based IHostedService

`ExpiryNotificationJob` is an `IHostedService` that on `StartAsync` calculates the delay until the first configured fire time (default `09:00`, configurable via `NOTIFY_TIME_UTC` env var), then loops with a 24-hour `PeriodicTimer`. `PeriodicTimer` is AOT-safe and avoids reflection.

*Alternative considered*: Quartz.NET / Hangfire. Rejected — heavy dependency, reflection-heavy internals, incompatible with AOT trimming.

*Alternative considered*: `Timer` + callback. Rejected — `PeriodicTimer` is the idiomatic .NET 6+ pattern and avoids callback re-entrancy.

### 4. Expiry classification (thresholds)

| Category | Condition |
|---|---|
| Expired | `exp_date < today` |
| Expires today | `exp_date == today` |
| Expires soon (≤ 3 days) | `today < exp_date <= today + 3` |
| Expires this week (4–7 days) | `today + 3 < exp_date <= today + 7` |

Only categories with at least one item appear in the message. Groups with no items in any category receive no message.

### 5. New repository method: GetItemsWithExpiryAsync

`ShoppingItemRepository` gains `GetItemsWithExpiryAsync(int groupId)` returning only items where `exp_date IS NOT NULL`. The notification service queries per group. A cross-group query is not used — it couples the query to group enumeration logic and makes unit testing harder.

### 6. /list display: expiry appended to item label

In `ShoppingListService.BuildListAsync`, items with `ExpDate != null` render as `• Name qty (до DD.MM)` in the text body. Button labels include the date: `✓ Name qty (до DD.MM)`. Items expiring today or already expired get a ⚠️ prefix in the text.

## Risks / Trade-offs

- **SQLite ALTER TABLE on startup** → If the app starts under high load and multiple instances run concurrently (Docker restart loop), two instances could both detect the missing column and both try to ALTER — SQLite will serialize them but the second will throw. Mitigation: catch and ignore `SqliteException` with message containing "duplicate column name".
- **BuySkipCallbackHandler refactor is a breaking dialog-state change** → Any in-flight dialog (state.Step == 2) at deploy time will behave correctly because the skip callback now advances to step 3 rather than finishing — users will just get an extra step. Mitigation: acceptable; dialogs are ephemeral in-memory state.
- **Daily fire time is UTC** → Groups in other timezones may get notifications at odd hours. Mitigation: documented in config; out of scope for this change.
- **No retry on Telegram send failure** → If the bot fails to send a notification to one group, that group is silently skipped until the next day. Mitigation: structured log at `Warning` level per failed group; a future change can add retry logic.

## Migration Plan

1. Deploy new binary — `DatabaseInitializer.StartAsync` detects missing `exp_date` column and runs ALTER TABLE.
2. Existing items get `NULL` expiry — no change in `/list` display (date is omitted when null).
3. `ExpiryNotificationJob` starts; on first day with no expiry data it sends nothing.
4. **Rollback**: revert binary. Old code ignores the `exp_date` column (it is never selected by old queries). No schema rollback needed.

## Open Questions

- Should items with `exp_date` in the past be auto-hidden from the shopping list, or left visible until manually removed? (Current design: left visible, shown with ⚠️.)
- Should the notification time be per-group or global? (Current design: global UTC time from env var.)
