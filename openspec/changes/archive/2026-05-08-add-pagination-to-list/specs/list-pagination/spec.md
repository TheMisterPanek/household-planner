## ADDED Requirements

### Requirement: View paginated shopping list with /list command and page number parameter
The system SHALL accept an optional page number parameter in the `/list` command. If provided, the bot displays the specified page; if omitted or invalid, it defaults to page 1. The list displays 10 items per page with navigation buttons.

#### Scenario: User sends /list without page number
- **WHEN** a group member sends `/list`
- **THEN** the bot displays page 1 of the shopping list (up to 10 items) with `[✓]` and `[✗ Убрать]` buttons per item

#### Scenario: User sends /list with explicit page number
- **WHEN** a group member sends `/list 2`
- **THEN** the bot displays page 2 of the shopping list (items 11-20) with pagination controls

#### Scenario: User requests page that exceeds available pages
- **WHEN** a group member sends `/list 99` but only 3 pages exist
- **THEN** the bot displays page 1 and appends "(showing page 1 of 3)" to indicate the requested page is out of range

#### Scenario: List pagination message persists
- **WHEN** `/list` is sent, a list message already exists, and no newer messages were posted after it
- **THEN** the bot edits the existing message in place to show the requested page

#### Scenario: Pagination message posted when chat has newer messages
- **WHEN** `/list` is sent, a list message exists, but newer messages have been posted after it
- **THEN** the bot posts a new list message for the requested page

---

### Requirement: Navigate list pages with Previous and Next buttons
The system SHALL provide `[← Previous]` and `[Next →]` buttons below the item list. Previous button appears on page 2+; Next button appears when more items exist beyond the current page. Buttons use callbacks that fit the 64-byte Telegram limit.

#### Scenario: Previous button on page 2
- **WHEN** user is viewing page 2+ of a list and taps `[← Previous]`
- **THEN** the list message edits to show the previous page with updated navigation buttons

#### Scenario: Previous button does not appear on page 1
- **WHEN** viewing page 1 of a list
- **THEN** the `[← Previous]` button is not displayed (no navigation needed)

#### Scenario: Next button on page with more items
- **WHEN** user is viewing a page where more items exist beyond the current 10
- **THEN** the `[Next →]` button is displayed; tapping it edits the message to show the next page

#### Scenario: Next button does not appear on last page
- **WHEN** viewing the final page of a list (no items beyond current 10)
- **THEN** the `[Next →]` button is not displayed

#### Scenario: Callback data fits 64-byte limit
- **WHEN** navigation buttons are rendered for a group
- **THEN** callback data is formatted as `list_prev:<groupId>` or `list_next:<groupId>` and does not exceed 64 bytes

---

### Requirement: Item action buttons work across all pages
The system SHALL ensure that item buttons (`[✓ Name qty]` and `[✗ Убрать]`) function identically on paginated lists as on non-paginated lists. Removing an item updates the list and resets to page 1.

#### Scenario: Tapping done button on paginated list
- **WHEN** user taps `[✓ Item Name]` on any page of a paginated list
- **THEN** the item is deleted, the list message updates to show page 1, and the price-capture dialog is initiated (unchanged from non-paginated behavior)

#### Scenario: Tapping remove button on paginated list
- **WHEN** user taps `[✗ Убрать]` on any page of a paginated list
- **THEN** the item is deleted, the list message updates to show page 1, and no dialog is initiated

#### Scenario: Item deletion shifts page contents
- **WHEN** user removes an item from page 2, leaving fewer items
- **WHEN** if the list now has fewer pages, the list resets to page 1
- **THEN** the redrawn message shows the correct subset of remaining items

---

### Requirement: List pagination footer displays page information
The system SHALL display page metadata in the list message footer showing current page number, total pages, and total items. Metadata is formatted using localized strings.

#### Scenario: Footer displays page info
- **WHEN** a paginated list is viewed
- **THEN** the footer includes "Page X of Y (Z total items)" where X = current page, Y = total pages, Z = total items

#### Scenario: Footer with single page
- **WHEN** a list has 10 or fewer items (fits on page 1)
- **THEN** the footer displays "Page 1 of 1 (N total items)" with no navigation buttons

#### Scenario: Footer localization
- **WHEN** a user in a Russian group views the list
- **THEN** the footer uses localized strings for page labels (e.g., "Страница" instead of "Page")

---

### Requirement: History records pagination context
The system SHALL record the page number and pagination metadata in the `ListViewed` history entry when the list is displayed.

#### Scenario: ListViewed history includes page number
- **WHEN** a group member sends `/list` or `/list 2`
- **THEN** a `ListViewed` history entry is recorded with payload including `{ "page": 2, "pageSize": 10, "totalItems": 45 }`

#### Scenario: Navigation button click records history
- **WHEN** a user taps `[← Previous]` or `[Next →]` to navigate pages
- **THEN** a `ListViewed` history entry is recorded for the new page with the current pagination context

