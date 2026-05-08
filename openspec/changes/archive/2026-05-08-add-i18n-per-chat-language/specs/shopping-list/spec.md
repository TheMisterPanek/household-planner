## MODIFIED Requirements

### Requirement: Add item to shopping list via /buy command
The system SHALL accept `/buy` in a group chat. If arguments are provided inline, the item is added immediately; otherwise a two-step dialog is initiated. All user-facing strings (prompts, confirmations, error messages, button labels) SHALL be produced via `ILocalizer.Get` using the chat's current language.

#### Scenario: User sends /buy with name and quantity inline
- **WHEN** a group member sends `/buy Молоко 2л`
- **THEN** the item is saved immediately and the bot confirms with a localized message of the form "{displayName} added {Name} {Quantity}"

#### Scenario: User sends /buy with name only inline
- **WHEN** a group member sends `/buy Молоко`
- **THEN** the item is saved immediately with no quantity and the bot confirms with a localized message of the form "{displayName} added {Name}"

#### Scenario: User completes full /buy dialog
- **WHEN** a group member sends `/buy` with no arguments
- **THEN** the bot replies with the localized prompt for "What to buy?" and waits for the next message from that user

#### Scenario: User enters item name
- **WHEN** the user replies with an item name after `/buy`
- **THEN** the bot asks the localized "How much?" prompt with a localized inline `[Skip]` button

#### Scenario: User enters quantity
- **WHEN** the user replies with a quantity string
- **THEN** the item is saved to the database and the bot confirms with a localized message of the form "{displayName} added {Name} {Quantity}"

#### Scenario: User skips quantity
- **WHEN** the user taps the localized `[Skip]` button
- **THEN** the item is saved with no quantity and the bot confirms with a localized message of the form "{displayName} added {Name}"

#### Scenario: /buy used in private chat
- **WHEN** a user sends `/buy` in a private chat
- **THEN** the bot replies with the localized "This command only works in group chats." message and does not start a dialog

---

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat showing all current items with inline action buttons. All strings in the list message and button labels SHALL be produced via `ILocalizer.Get` using the chat's current language.

#### Scenario: List has items
- **WHEN** a group member sends `/list`
- **THEN** the bot posts (or edits the existing) list message with a localized header and one inline button row per item: a localized check button and a localized remove button

#### Scenario: List is empty
- **WHEN** a group member sends `/list` and no items exist
- **THEN** the bot posts the localized "Shopping list is empty" message with no buttons

#### Scenario: Subsequent /list call and list message is still last
- **WHEN** `/list` is called, a `ListMessageId` is already saved, and no messages were sent after it
- **THEN** the bot edits the existing message in place

#### Scenario: Subsequent /list call and chat has newer messages
- **WHEN** `/list` is called, a `ListMessageId` is already saved, but newer messages exist after it in the chat
- **THEN** the bot posts a new list message and saves the new `MessageId` to the group

#### Scenario: Edit limit exceeded
- **WHEN** editing the list message returns a Telegram 400 error (48h limit)
- **THEN** the bot posts a new message and saves the new `MessageId` to the group

---

### Requirement: Mark shopping item as bought
The system SHALL remove the item from the shopping list and confirm when a user taps the check button. The confirmation message SHALL be localized to the chat's language.

#### Scenario: User marks item bought
- **WHEN** a user taps the check button on a list item
- **THEN** the item is deleted from the database, the list message is updated, and the bot sends a localized confirmation of the form "{displayName}: {Name} {Quantity} — marked ✓"

---

### Requirement: Remove shopping item without marking bought
The system SHALL delete the item from the list when the user taps the remove button.

#### Scenario: User removes item
- **WHEN** a user taps the localized remove button on a list item
- **THEN** the item is deleted from the database and the list message is updated immediately
