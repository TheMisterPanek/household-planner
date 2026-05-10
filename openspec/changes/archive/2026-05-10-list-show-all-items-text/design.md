## Context

Currently, `ShoppingListService.BuildListAsync()` formats the shopping list message with:
- A simple header text ("📋 Shopping List")
- Inline buttons for each item (with delete action)
- Pagination buttons for large lists

Users request visibility of all items at once without needing to read button labels. The message body is underutilized — it should include a formatted text list of all items.

## Goals / Non-Goals

**Goals:**
- Add a bullet-point text list of all shopping items to the message body
- Display item name and quantity (if available) for each item
- Keep inline buttons for delete and pagination actions unchanged
- Support pagination: show all items for the current page in text form
- Use localized labels via ILocalizer for consistency

**Non-Goals:**
- Redesigning button layout or interaction model
- Storing list display preferences (always show all items)
- Changing the underlying data model or persistence
- Adding new capabilities like search or filtering

## Decisions

### Decision 1: Format text list in message body before buttons

**Chosen**: Message body contains header + bullet list, buttons appear below.

**Why**: Clear visual hierarchy. Users see the list first (what), actions second (how to manage). Follows telegram message conventions.

**Alternative considered**: Interleave list items with buttons. Rejected: harder to read, buttons lose context.

### Decision 2: Format one item per line with name and optional quantity

**Chosen**: `• Item Name (Qty)` on separate lines; quantity in parentheses only if provided.

**Why**: Consistent with standard bullet-list UI. Quantity field helps distinguish items with same name but different amounts.

**Alternative considered**: Inline quantity like "Item Name x2". Rejected: bullet-point format more readable, "x2" semantically confusing.

### Decision 3: Show items for current page only in text list

**Chosen**: When pagination is active, text list contains only items on the current page (same as button pagination).

**Why**: Consistency. Text and buttons show the same items, no surprises. Full list would be confusing with "Page 1 of 3" label.

**Alternative considered**: Always show all items in text, buttons show only current page. Rejected: violates principle of least surprise; users see different content in text vs. buttons.

### Decision 4: Modify `ShoppingListService.BuildListAsync()` to concatenate text list

**Chosen**: Update the return value to include formatted item list in the message text.

**Why**: Single responsibility — service builds the message, handler sends it. No new abstractions needed.

**Alternative considered**: New method `FormatItemListAsText()` in ShoppingListService. Rejected: adds surface area; simpler to extend existing method.

## Risks / Trade-offs

- **Message length**: Telegram has a 4096-character limit. For very large lists (100+ items), text + buttons could exceed limit. Mitigation: Pagination already limits items per page; current pagination settings should keep messages under limit. Monitor in testing.
- **No breaking changes**: Fully backward-compatible; handlers don't change.

## Migration Plan

1. Update `ShoppingListService.BuildListAsync()` to format item list as text
2. Add localization key for bullet list (e.g., `list.item-format`)
3. Test with various list sizes to ensure message stays under 4096 characters
4. No database migration needed
5. Rollback: Revert service formatting code, no data cleanup required
