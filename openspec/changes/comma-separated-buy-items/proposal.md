## Why

Users frequently need to add multiple simple items to the shopping list (e.g., milk, eggs, bread). Currently, they must either send multiple `/buy` commands or use the dialog flow multiple times. Supporting comma-separated input reduces friction and matches common user expectations.

## What Changes

- The `/buy` command now accepts comma-separated items as a single invocation
- Each comma-separated item is trimmed and added as a separate shopping list entry
- Quantity syntax is preserved: `/buy milk 1L, eggs 2` will add "milk" with qty "1L" and "eggs" with qty "2"
- The bot confirms all items with a summary: "Иван добавил(а) 3 товара: молоко, яйца, хлеб"
- Dialog flow unchanged: `/buy` with no args still opens the two-step dialog for single-item entry

## Capabilities

### New Capabilities
<!-- None - this is a modification, not a new capability -->

### Modified Capabilities
- `shopping-list`: The `/buy` command now parses comma-separated items and adds each as an individual shopping list entry. History recording updated to reflect bulk additions.

## Impact

- **Affected code**: `BuyCommandHandler`, `ShoppingListService` (new parsing logic)
- **Database**: No schema changes; uses existing shopping list tables
- **Localization**: New confirmation message template for bulk additions (e.g., "added 3 items")
- **Tests**: Unit tests for CSV parsing logic; integration tests for bulk `/buy` scenarios
- **API surface**: `/buy` command behavior extended (backward compatible)

## Rollback Plan

Revert parsing logic to treat entire input as single item name if needed. No data migration required.

## Cross-cutting Decisions

- Uses `System.Text.Json` for consistent serialization (if storing bulk operation metadata)
- Follows existing item-added history recording pattern with new bulk-add record type
- Dialog flow intentionally unchanged — only inline `/buy` with args supports CSV
