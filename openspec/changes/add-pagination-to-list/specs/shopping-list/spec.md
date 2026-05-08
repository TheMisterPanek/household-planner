## MODIFIED Requirements

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat. The list now supports pagination, displaying up to 10 items per page with navigation controls. The `/list` command accepts an optional page number parameter (defaults to page 1).

#### Scenario: List has items on page 1
- **WHEN** a group member sends `/list`
- **THEN** the bot posts (or edits the existing) list message with header "🛒 Список покупок:", items for page 1 with inline buttons `[✓ Name qty]` and `[✗ Убрать]` per item, page footer "Page 1 of X (N total items)", and navigation buttons `[Next →]` if more pages exist

#### Scenario: List has items on page 2
- **WHEN** a group member sends `/list 2`
- **THEN** the bot displays page 2 items (items 11-20) with `[← Previous]` button (since this is not page 1) and `[Next →]` button if page 3 exists

#### Scenario: List is empty
- **WHEN** a group member sends `/list` and no items exist
- **THEN** the bot posts "Список покупок пуст" with no buttons or pagination controls

#### Scenario: Subsequent /list call and list message is still last
- **WHEN** `/list` is called, a `ListMessageId` is already saved, and no messages were sent after it
- **THEN** the bot edits the existing message in place to show the requested page (default page 1)

#### Scenario: Subsequent /list call and chat has newer messages
- **WHEN** `/list` is called, a `ListMessageId` is already saved, but newer messages exist after it in the chat
- **THEN** the bot posts a new list message (page 1) and saves the new `MessageId` to the group

#### Scenario: Edit limit exceeded
- **WHEN** editing the list message returns a Telegram 400 error (48h limit)
- **THEN** the bot posts a new message and saves the new `MessageId` to the group

#### Scenario: Invalid page number defaults to page 1
- **WHEN** a group member sends `/list 99` and only 3 pages exist
- **THEN** the bot displays page 1 with a note appended: "(showing page 1 of 3)"

