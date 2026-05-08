## 1. Service Layer - Pagination Logic

- [ ] 1.1 Add `GetPagedItemsAsync(groupId, pageNumber, pageSize)` method to ShoppingListService with unit tests
- [ ] 1.2 Implement page calculation: determine total pages, validate page number, slice items for current page
- [ ] 1.3 Unit test: Valid page number returns correct slice of items
- [ ] 1.4 Unit test: Page number exceeding max defaults to page 1
- [ ] 1.5 Unit test: Empty list returns empty items and page count of 1
- [ ] 1.6 Unit test: Exactly 10 items (one page) returns correct count

## 2. Command Handler - Parameter Parsing

- [ ] 2.1 Update ListCommandHandler to parse optional page parameter from command text (e.g., `/list 2`)
- [ ] 2.2 Add validation: reject non-numeric or negative page numbers; log warning and use page 1
- [ ] 2.3 Pass page number to ShoppingListService.GetPagedItemsAsync()
- [ ] 2.4 Unit test: `/list` command without page number uses page 1
- [ ] 2.5 Unit test: `/list 2` command parses page number correctly
- [ ] 2.6 Unit test: Invalid page number (`/list abc`) logs warning and defaults to page 1
- [ ] 2.7 Unit test: ListCommandHandler routes page parameter to service correctly

## 3. Callback Handlers - Pagination Navigation

- [ ] 3.1 Implement `list_prev` callback handler with ICallbackHandler
- [ ] 3.2 Implement `list_next` callback handler with ICallbackHandler
- [ ] 3.3 Add validation: verify callback data contains valid groupId; log and ignore on invalid data
- [ ] 3.4 Calculate previous/next page number and call pagination service
- [ ] 3.5 Edit list message in place with new page content
- [ ] 3.6 Unit test: `list_prev` callback decrements page number and re-renders list
- [ ] 3.7 Unit test: `list_next` callback increments page number and re-renders list
- [ ] 3.8 Unit test: Callback handlers respect item button click behavior (marks item bought/removed, resets to page 1)

## 4. Message Formatting - Pagination UI

- [ ] 4.1 Update list message formatter to display current page items only (10 items max)
- [ ] 4.2 Add pagination buttons: `[← Previous]` if page > 1, `[Next →]` if more items exist
- [ ] 4.3 Use callback format `list_prev:<groupId>` and `list_next:<groupId>` for buttons
- [ ] 4.4 Verify callback data does not exceed 64-byte Telegram limit
- [ ] 4.5 Add page footer with localized text: "Page X of Y (Z total items)"
- [ ] 4.6 Unit test: Formatter includes Previous button only on page 2+
- [ ] 4.7 Unit test: Formatter includes Next button only when items remain
- [ ] 4.8 Unit test: Footer displays correct page number, total pages, and total items count
- [ ] 4.9 Unit test: Message layout with pagination matches expected format

## 5. Localization - Pagination Strings

- [ ] 5.1 Add localization keys for pagination UI in default (English) and Russian locales:
  - `pagination.page_label` (e.g., "Page")
  - `pagination.of_label` (e.g., "of")
  - `pagination.items_label` (e.g., "items")
  - `pagination.previous_button` (e.g., "← Previous")
  - `pagination.next_button` (e.g., "Next →")
  - `pagination.out_of_range` (e.g., "showing page 1 of {maxPage}")

- [ ] 5.2 Update ILocalizer calls in list formatter to use pagination keys
- [ ] 5.3 Verify no hardcoded pagination strings remain in handlers

## 6. History Audit - Pagination Context

- [ ] 6.1 Update ListViewed history payload to include pagination metadata: `{ "page": X, "pageSize": 10, "totalItems": Y }`
- [ ] 6.2 Ensure ListCommandHandler records history with page info after list is posted/edited
- [ ] 6.3 Ensure callback handlers (prev/next) record ListViewed history for navigation actions
- [ ] 6.4 Wrap history recording in try/catch; log at Warning on failure but don't suppress UI response
- [ ] 6.5 Unit test: ListViewed history includes correct page number and total items

## 7. Integration Testing

- [ ] 7.1 Integration test: Full flow `/list` command with pagination displays correct items and buttons
- [ ] 7.2 Integration test: Pagination navigation (prev/next buttons) works end-to-end
- [ ] 7.3 Integration test: Item removal on paginated list resets to page 1
- [ ] 7.4 Integration test: Message edit vs. post strategy works with pagination
- [ ] 7.5 Integration test: History is recorded for /list command and pagination navigation

## 8. Manual Testing & Verification

- [ ] 8.1 Test `/list` command with 0 items (empty list)
- [ ] 8.2 Test `/list` command with 1-10 items (single page, no navigation buttons)
- [ ] 8.3 Test `/list` command with 11+ items (multiple pages, navigation buttons visible)
- [ ] 8.4 Test `/list 2` command to jump to page 2
- [ ] 8.5 Test `/list 99` with out-of-range page number (should show page 1 with note)
- [ ] 8.6 Test Previous/Next buttons navigate correctly and update message in place
- [ ] 8.7 Test item removal on page 2 → page resets to 1
- [ ] 8.8 Test Telegram 48h edit limit: old list message can't be edited → bot posts new message
- [ ] 8.9 Test callback data size does not exceed 64 bytes (use debugger or logging)
- [ ] 8.10 Verify localization strings appear correctly for Russian groups

