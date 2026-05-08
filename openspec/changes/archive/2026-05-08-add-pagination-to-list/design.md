## Context

The `/list` command currently displays all shopping list items in a single message. For groups with many items (20+), this results in very tall messages that are cumbersome on mobile and create chat clutter. Pagination will break the list into manageable pages while maintaining the existing persistent-message strategy and in-line action buttons.

The core architecture (Clean Architecture, DI, Telegram command/callback dispatch) and persistent message strategy remain unchanged. Pagination is purely a presentation concern — no data model changes needed.

## Goals / Non-Goals

**Goals:**
- Allow users to navigate large shopping lists in pages
- Keep page navigation intuitive with previous/next buttons
- Maintain backward compatibility: `/list` without page number shows page 1
- Keep callback data within Telegram's 64-byte limit
- Record page number in history for audit trail

**Non-Goals:**
- Persistent page state per user (no "remember my last page" feature)
- Configurable page sizes (fixed at 10 items per page)
- Filtering or sorting by pagination parameter (pagination is independent of other list features)

## Decisions

**Decision 1: Page size = 10 items**
- **Rationale**: 10 items fit on most mobile screens without scrolling; balances granularity with message length
- **Alternatives**: Configurable size (adds complexity and DB column), variable size based on screen size (not visible to bot)
- **AOT Safe**: ✓ (constant, no reflection)

**Decision 2: Page navigation via button callbacks, not command parameters**
- **Rationale**: Buttons provide better UX than re-typing `/list 2`; buttons stay with the message
- **Callbacks**: `list_prev:<groupId>` and `list_next:<groupId>` (fits 64-byte limit; 24 bytes max for typical groupId)
- **Alternatives**: Store page in session state (adds state management complexity), embed page in callback data (already using session tokens for other flows, follow that pattern if data grows)
- **AOT Safe**: ✓ (string callbacks, no dynamic code)

**Decision 3: Stateless page calculation**
- **Rationale**: Page number is a view-time request parameter; calculate from current items and requested page
- **Behavior**: If page number exceeds available pages (e.g., items removed, user requests page 99), show page 1 with a note "Page 1 of X (showing all newer pages removed items)"
- **Alternatives**: Store current page in Group row (adds schema + migration), cache page state in memory (loses state on restart)
- **Trade-off**: If items are added/removed between page requests, page numbers shift; this is acceptable since lists are frequently updated

**Decision 4: No new database schema**
- **Rationale**: Page number is computed at display time; no state to persist
- **Implementation**: Fetch all items (existing query), slice in memory (LINQ), render slice
- **Alternatives**: Pagination in SQL (requires offset/limit in query; SQLITE supports LIMIT/OFFSET)
- **Trade-off**: Memory usage is bounded (typical lists are <100 items); SQL pagination would require query changes and complicates the persistence layer

**Decision 5: History records page number in payload**
- **Rationale**: Audit trail should capture which page was viewed (useful for understanding user behavior)
- **Format**: `ListViewed` history payload includes `{ "page": 1, "pageSize": 10, "totalItems": 45 }`
- **Alternatives**: No page info (loses context), page info in message field (less structured)

**Decision 6: Edit/repost strategy unchanged**
- **Rationale**: The persistent message strategy (edit if most recent, post new if newer messages exist) applies to paginated lists too
- **Each page is its own message**: When user requests page 2, if list message exists, edit it to show page 2; subsequent page requests continue editing the same message

## Risks / Trade-offs

**[Risk] Page number invalidation when items removed**
- If user is on page 3, then items are removed and only 2 pages exist, requesting page 3 shows page 1
- **Mitigation**: Display clear feedback ("Page 1 of 2") so user understands; resetting to page 1 is intuitive for a frequently-changing list

**[Risk] Callback data size for large group IDs**
- Callback format `list_prev:<groupId>` could exceed 64 bytes for IDs with many digits
- **Mitigation**: Typical Telegram group IDs are ~10 digits (60 bytes total); if this becomes an issue, use a short hash/token stored in session (follow existing session pattern for price dialogs)

**[Risk] Race condition: item deleted between page view and button click**
- User taps ✓ on page 3, but items on page 2 were removed, shifting page numbers
- **Mitigation**: Item deletion immediately updates the list (replaces/edits message); page state is ephemeral, not persisted. This is acceptable — the user's ✓ click works regardless of page state (it identifies the item by ID, not page position)

**[Trade-off] In-memory vs SQL pagination**
- In-memory slicing is simpler and avoids schema changes, but fetches all items each time
- For typical lists (<100 items), this is negligible; for very large lists (1000+ items), SQL LIMIT/OFFSET would be more efficient
- **Decision**: Stay with in-memory for simplicity; profile if performance becomes an issue

## Migration Plan

1. Add `GetPagedItemsAsync(groupId, pageNumber, pageSize)` to `ShoppingListService`
2. Update `ListCommandHandler` to parse optional page parameter and call the new service method
3. Update Telegram message formatter to include pagination buttons and page footer
4. Add `list_prev` and `list_next` callback handlers
5. Update `ListViewed` history payload to include page info
6. No database migration needed
7. **Rollback**: Remove button handlers; revert ListCommandHandler to show all items; revert history payload

## Open Questions

- Should page size be configurable via environment variable or settings? (Defer to future; default to 10 for now)
- Should we add a "go to page X" shortcut button? (Defer; pagination buttons are sufficient MVP)
