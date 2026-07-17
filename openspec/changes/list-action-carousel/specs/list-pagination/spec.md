## MODIFIED Requirements

### Requirement: View paginated shopping list with /list command and page number parameter
The system SHALL accept an optional page number parameter in the `/list` command. If provided, the bot displays the specified page; if omitted or invalid, it defaults to page 1. The list displays 2 items per page (action-button carousel) with navigation buttons; the message's plain-text item listing always shows every item regardless of the current page.

#### Scenario: User sends /list without page number
- **WHEN** a group member sends `/list`
- **THEN** the bot displays page 1 of the shopping list (up to 2 items as action buttons) with `[✓]` and `[✗ Убрать]` buttons per item, alongside a plain-text listing of all items in the message body

#### Scenario: User sends /list with explicit page number
- **WHEN** a group member sends `/list 2`
- **THEN** the bot displays page 2 of the action-button carousel (items 3-4) with pagination controls

#### Scenario: User requests page that exceeds available pages
- **WHEN** a group member sends `/list 99` but only 3 carousel pages exist
- **THEN** the bot displays page 1 and appends "(showing page 1 of 3)" to indicate the requested page is out of range

#### Scenario: List pagination message persists
- **WHEN** `/list` is sent, a list message already exists, and no newer messages were posted after it
- **THEN** the bot edits the existing message in place to show the requested page

#### Scenario: Pagination message posted when chat has newer messages
- **WHEN** `/list` is sent, a list message exists, but newer messages have been posted after it
- **THEN** the bot posts a new list message for the requested page

---

### Requirement: List pagination footer displays page information
The system SHALL display page metadata in the list message footer showing current page number, total pages, and total items, reflecting the action-button carousel's pagination (2 items per page), not the full text listing. Metadata is formatted using localized strings.

#### Scenario: Footer displays page info
- **WHEN** a paginated list is viewed
- **THEN** the footer includes "Page X of Y (Z total items)" where X = current carousel page, Y = total carousel pages (at 2 items per page), Z = total items

#### Scenario: Footer with single page
- **WHEN** a list has 2 or fewer items (fits on carousel page 1)
- **THEN** the footer displays "Page 1 of 1 (N total items)" with no navigation buttons

#### Scenario: Footer localization
- **WHEN** a user in a Russian group views the list
- **THEN** the footer uses localized strings for page labels (e.g., "Страница" instead of "Page")

---

### Requirement: History records pagination context
The system SHALL record the page number and pagination metadata in the `ListViewed` history entry when the list is displayed, using the action-button carousel's page size.

#### Scenario: ListViewed history includes page number
- **WHEN** a group member sends `/list` or `/list 2`
- **THEN** a `ListViewed` history entry is recorded with payload including `{ "page": 2, "pageSize": 2, "totalItems": 45 }`

#### Scenario: Navigation button click records history
- **WHEN** a user taps `[← Previous]` or `[Next →]` to navigate carousel pages
- **THEN** a `ListViewed` history entry is recorded for the new page with the current pagination context
