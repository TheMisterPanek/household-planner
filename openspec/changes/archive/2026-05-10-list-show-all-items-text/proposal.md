## Why

The shopping list message currently displays items only as inline button options, making it difficult for users to see the complete list at a glance. Users must scroll through buttons to see all items. Adding a text-based bullet list in the message body provides immediate visibility of all items and improves the user experience.

## What Changes

- Shopping list message body now includes a formatted bullet list with all shopping items
- Each item shows its name and quantity (if provided)
- Inline buttons remain for delete and pagination actions
- Message format: text header with bullet list, followed by action buttons below

## Capabilities

### New Capabilities
- `list-text-display`: Display all shopping items as a formatted bullet list in the message body

### Modified Capabilities
- `shopping-list`: The list display now includes all items as text in the message content, not just as buttons

## Impact

- **Code**: `ShoppingListService.BuildListAsync()` method to format item list as text
- **UI**: Shopping list message now has richer content with text list + buttons
- **Rollback**: Remove text list formatting, revert to button-only display
- **Affected teams**: Backend (service layer)
- **AOT compatibility**: No reflection or dynamic code; straightforward string formatting
- **Cross-cutting**: Uses existing ILocalizer for labels, no new persistence layer needed
