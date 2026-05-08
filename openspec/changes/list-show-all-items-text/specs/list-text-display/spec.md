# List Text Display Capability

## ADDED Requirements

### Requirement: Display all items as bullet list
The shopping list message SHALL include a formatted bullet-point list of all items currently displayed (respecting pagination), placed in the message body above action buttons.

#### Scenario: Single page list displays all items as text
- **WHEN** user views a shopping list with 5 items on a single page
- **THEN** the message body displays all 5 items as bullet points with names and quantities

#### Scenario: Paginated list shows current page items
- **WHEN** user views page 2 of a 3-page list with 8 items total
- **THEN** the message body displays only the items on page 2 as bullet points

#### Scenario: Item with quantity displays correctly
- **WHEN** a shopping list contains "Milk (2L)" 
- **THEN** the bullet list shows the item as "• Milk (2L)"

#### Scenario: Item without quantity displays name only
- **WHEN** a shopping list contains "Bread" without quantity
- **THEN** the bullet list shows the item as "• Bread"

### Requirement: Format items with localized labels
The system SHALL use ILocalizer to resolve item format labels, ensuring consistency with configured language preferences.

#### Scenario: Items display with correct language
- **WHEN** a group has Russian language preference
- **THEN** item labels in the bullet list use Russian text (if applicable)

#### Scenario: Missing localization keys fall back gracefully
- **WHEN** a localization key is missing
- **THEN** the system logs a warning and uses the key name as fallback

### Requirement: Message respects Telegram size limits
The list message SHALL remain under Telegram's 4096-character limit when combined with buttons.

#### Scenario: Large list stays within limit
- **WHEN** a list contains 50 items paginated across 5 pages
- **THEN** each page's message (text + buttons) is under 4096 characters
