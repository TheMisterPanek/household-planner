## Context

`/buy` and `/list` are implemented. The list message renders one keyboard row per item:

```
[✓ Молоко 2л]  [✗ Убрать]
```

The button callback data uses prefixes already allocated in the `shopping-list-commands` design:
- `shop:done:<itemId>` — mark item as bought
- `shop:remove:<itemId>` — silently remove item

`UpdateDispatcher` is already wired to route `CallbackQuery` updates to `ICallbackHandler` by prefix. `ShoppingListService` already knows how to build the updated message and edit it in place. These two handlers are the only missing piece.

## Goals / Non-Goals

**Goals:**
- Implement `ShopDoneCallbackHandler` and `ShopRemoveCallbackHandler` as `ICallbackHandler`
- Delete the item, refresh the list message, answer the callback query (to dismiss the Telegram spinner)
- Handle stale callbacks gracefully (item already deleted — treat as no-op)

**Non-Goals:**
- New callback prefixes or button types
- Undo / confirmation dialogs
- Analytics or audit logging of item deletions

## Decisions

### D1: Parse item ID from callback data with `string.Split`

**Decision:** Extract `itemId` as `int.Parse(data.Split(':')[2])`.

**Rationale:** Callback data is `"shop:done:42"` — three fixed segments, no ambiguity. `string.Split` + `int.Parse` is AOT-safe, zero-allocation for this size, and requires no regex or JSON deserialization.

**Alternative considered:** Encoding item data as JSON in callback data — unnecessary overhead; Telegram callback data limit is 64 bytes and the ID alone is sufficient.

### D2: Answer callback query before editing the message

**Decision:** Call `AnswerCallbackQueryAsync` first (dismisses the Telegram loading spinner), then delete + refresh.

**Rationale:** If `EditMessageTextAsync` fails (e.g., 48h edit limit), the user still sees the spinner resolve. The same 400-error fallback from `ListCommandHandler` applies: catch `ApiRequestException`, repost list, save new `ListMessageId`.

### D3: Stale callback = silent no-op

**Decision:** If `ShoppingItemRepository.DeleteAsync` affects 0 rows (item already gone), still call `ShoppingListService` to refresh the list and answer the callback query — no error message to the user.

**Rationale:** Stale callbacks happen when two users tap the same button simultaneously. Sending an error ("item not found") is confusing; silently refreshing the list to its current state is the correct UX.

### D4: `ShopDoneCallbackHandler` sends a group confirmation; `ShopRemoveCallbackHandler` does not

**Decision:** Done handler sends `"<FirstName>: <Name> <Qty> — отмечено ✓"` as a regular group message. Remove handler only refreshes the list.

**Rationale:** "Bought" is a social signal worth broadcasting; "remove" is a correction that needs no fanfare.

## Risks / Trade-offs

- **Concurrent taps on the same item** → Both handlers will call `DeleteAsync`; second call is a no-op (0 rows affected). Both will then refresh the list — the second refresh is redundant but harmless.
- **Telegram 48h edit limit on list message** → Mitigated by the same repost fallback already in `ShoppingListService`.
- **Callback data exceeds 64 bytes** → Not possible: `"shop:done:"` (10 chars) + max int (10 chars) = 20 chars, well within limit.

## Migration Plan

1. Add `ShopDoneCallbackHandler.cs` and `ShopRemoveCallbackHandler.cs` under `Handlers/`
2. Register both as `ICallbackHandler` in `Program.cs`
3. Deploy — no schema changes, no data migration

**Rollback:** Remove DI registrations and delete handler files. `UpdateDispatcher` already ignores unknown prefixes.
