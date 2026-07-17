## MODIFIED Requirements

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat showing all current items with inline action buttons. When the group has at least one item with a non-null `Category`, the message SHALL include additional rows of category filter buttons, wrapped to at most 2 buttons per row. Non-interactive spacer rows SHALL separate the item action-button block from the pagination row (when present) and from the filter button block (when present).

#### Scenario: List has items
- **WHEN** a group member sends `/list`
- **THEN** the bot posts (or edits the existing) list message with header "🛒 Список покупок:" and one inline button row per item: `[✓ Name qty]` and `[✗ Убрать]`

#### Scenario: List is empty
- **WHEN** a group member sends `/list` and no items exist
- **THEN** the bot posts "Список покупок пуст" with no buttons

#### Scenario: Subsequent /list call and list message is still last
- **WHEN** `/list` is called, a `ListMessageId` is already saved, and no messages were sent after it
- **THEN** the bot edits the existing message in place

#### Scenario: Subsequent /list call and chat has newer messages
- **WHEN** `/list` is called, a `ListMessageId` is already saved, but newer messages exist after it in the chat
- **THEN** the bot posts a new list message and saves the new `MessageId` to the group

#### Scenario: Edit limit exceeded
- **WHEN** editing the list message returns a Telegram 400 error (48h limit)
- **THEN** the bot posts a new message and saves the new `MessageId` to the group

#### Scenario: List with no categorized items shows no filter row
- **WHEN** a group member sends `/list` and no item in the group has a `Category` set
- **THEN** the list message renders with no category filter rows

#### Scenario: List with categorized items shows wrapped filter rows
- **WHEN** a group member sends `/list` and at least one item has a `Category` set, with 5 categories plus "Все" active (6 buttons total)
- **THEN** the filter buttons render across 3 rows of at most 2 buttons each, rather than one crowded row

#### Scenario: Spacer row separates item actions from pagination
- **WHEN** the list has more than one carousel page of items (see `list-pagination`)
- **THEN** a non-interactive spacer row is inserted between the last item action-button row and the pagination row

#### Scenario: Spacer row separates pagination (or item actions) from filters
- **WHEN** the list includes a category filter row and either a pagination row or item action rows precede it
- **THEN** a non-interactive spacer row is inserted immediately before the first filter button row

#### Scenario: No spacer when the following block is absent
- **WHEN** the list has only one carousel page (no pagination row) and no categorized items (no filter row)
- **THEN** no spacer rows are rendered — only the item action rows and the final Cancel row

---

## ADDED Requirements

### Requirement: Spacer buttons are visually present but functionally inert
The system SHALL render spacer rows as a single full-width button with a neutral, localized label. Tapping a spacer button SHALL acknowledge the Telegram callback (clearing the client-side loading indicator) and SHALL NOT change any list state, item state, or dialog state.

#### Scenario: Tapping a spacer button does nothing
- **WHEN** a user taps a spacer row's button on the list message
- **THEN** the callback query is answered (no loading spinner persists) and the list message, item state, and any pending dialogs are left completely unchanged
