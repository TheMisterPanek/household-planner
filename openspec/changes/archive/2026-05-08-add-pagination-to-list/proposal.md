## Why

Shopping lists can grow large, making them difficult to read on mobile devices and requiring extensive scrolling. Pagination allows users to navigate list items in manageable chunks, improving UX and reducing message clutter in group chats.

## What Changes

- `/list` command now accepts an optional page number parameter (defaults to page 1)
- List message displays only items for the current page (configurable items per page)
- Add pagination controls: `[← Previous]` and `[Next →]` buttons between item rows and list footer
- Display current page info in footer: "Page X of Y (N total items)"
- Mark item buttons (✓ and ✗) continue to work — removing an item updates the list and resets to page 1
- Non-existent page numbers (e.g., `/list 99`) display page 1 with a note

## Capabilities

### New Capabilities
- `list-pagination`: Pagination for the shopping list display, allowing users to navigate large lists via page parameters and navigation buttons

### Modified Capabilities
- `shopping-list`: The "View shared shopping list via /list command" requirement now includes pagination support; the persistent message strategy remains unchanged but the message content and button layout are updated to show paginated items and page navigation controls

## Impact

- **ShoppingListService**: Add `GetPagedItemsAsync(groupId, pageNumber, pageSize)` method
- **ListCommandHandler**: Parse optional page parameter from command text; pass page number to service
- **Telegram message formatting**: Replace item list with paginated subset; add previous/next navigation buttons
- **Button callbacks**: Add handlers for `list_prev:<groupId>` and `list_next:<groupId>` to navigate pages
- **Database**: No schema changes (pagination is in-memory based on existing items)
- **History**: ListViewed history continues to record which page was viewed (payload includes page number)

