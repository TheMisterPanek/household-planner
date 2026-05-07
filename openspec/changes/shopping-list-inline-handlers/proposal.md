## Why

`/buy` and `/list` are complete, but tapping the `[✓ Name qty]` and `[✗ Убрать]` buttons does nothing — the inline keyboard is rendered but the callback handlers are missing. Without them the shopping list is read-only after posting.

## What Changes

- Add `ShopDoneCallbackHandler` (`CallbackPrefix = "shop:done:"`) — deletes the item, refreshes the list message, sends a bought confirmation to the group
- Add `ShopRemoveCallbackHandler` (`CallbackPrefix = "shop:remove:"`) — deletes the item and refreshes the list message silently
- Register both handlers as `ICallbackHandler` in `Program.cs`

## Capabilities

### New Capabilities
_(none)_

### Modified Capabilities
- `shopping-list`: Inline button requirements were already specified; this change delivers the missing implementation so the full interaction loop is functional

## Impact

- **Code**: Two new handler files under `Handlers/`; two new DI registrations in `Program.cs`
- **Dependencies**: None — reuses `ShoppingListService`, `ICallbackHandler`, and `UpdateDispatcher` already in place
- **AOT**: Callback data is parsed with `string.Split` / `int.Parse` — no reflection, fully AOT-safe
- **Rollback**: Remove the two DI registrations and delete the handler files; `UpdateDispatcher` already silently ignores unknown callback prefixes
- **Teams**: Single developer
