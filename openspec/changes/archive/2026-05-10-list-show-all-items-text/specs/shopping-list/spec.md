# Shopping List Capability - Delta

## MODIFIED Requirements

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat. The message body SHALL include a header and a formatted bullet-point list of all current items (respecting pagination), followed by inline action buttons for each item.

#### Scenario: List has items on single page
- **WHEN** a group member sends `/list` and the list has 5 items
- **THEN** the bot posts (or edits the existing) list message with:
  - Header text "📋 Shopping List"
  - Bullet list: "• Item1\n• Item2\n• Item3\n• Item4\n• Item5"
  - One inline button row per item: `[✓ Item qty]` and `[✗ Remove]`

#### Scenario: List has items across multiple pages
- **WHEN** a group member sends `/list page 2` and items are paginated
- **THEN** the bot posts/edits the list message with:
  - Header text "📋 Shopping List"
  - Bullet list containing only items on page 2
  - Pagination buttons below action buttons

#### Scenario: List is empty
- **WHEN** a group member sends `/list` and no items exist
- **THEN** the bot posts "📋 Shopping List is empty" with no buttons

#### Scenario: Item with quantity displays in list
- **WHEN** an item "Milk (2L)" is in the list
- **THEN** the bullet list shows "• Milk (2L)"

#### Scenario: Item without quantity displays in list
- **WHEN** an item "Bread" (no quantity) is in the list
- **THEN** the bullet list shows "• Bread"

#### Scenario: Subsequent /list call and list message is still last
- **WHEN** `/list` is called, a `ListMessageId` is already saved, and no messages were sent after it
- **THEN** the bot edits the existing message in place with updated bullet list

#### Scenario: Subsequent /list call and chat has newer messages
- **WHEN** `/list` is called, a `ListMessageId` is already saved, but newer messages exist after it in the chat
- **THEN** the bot posts a new list message with the updated bullet list and saves the new `MessageId` to the group

#### Scenario: Edit limit exceeded
- **WHEN** editing the list message returns a Telegram 400 error (48h limit)
- **THEN** the bot posts a new message with the updated bullet list and saves the new `MessageId` to the group

#### Scenario: Message stays within size limits
- **WHEN** a list is displayed with 50 items paginated across 5 pages
- **THEN** each page's message (header + bullet list + buttons) remains under 4096 characters
