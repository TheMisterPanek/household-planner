## MODIFIED Requirements

### Requirement: Add item to shopping list via /buy command
The system SHALL accept `/buy` in a group chat. If arguments are provided inline, the item is added immediately (without an expiry-date step); otherwise a three-step dialog is initiated: name → quantity → expiry date.

#### Scenario: User sends /buy with name and quantity inline
- **WHEN** a group member sends `/buy Молоко 2л`
- **THEN** the item is saved immediately with no expiry date and the bot confirms "Иван добавил(а) Молоко 2л" (last token = quantity, everything before = name)

#### Scenario: User sends /buy with name only inline
- **WHEN** a group member sends `/buy Молоко`
- **THEN** the item is saved immediately with no quantity and no expiry date and the bot confirms "Иван добавил(а) Молоко"

#### Scenario: User completes full /buy dialog
- **WHEN** a group member sends `/buy` with no arguments
- **THEN** the bot replies "Что купить?" and waits for the next message from that user

#### Scenario: User enters item name
- **WHEN** the user replies with an item name after `/buy`
- **THEN** the bot asks "Сколько?" with an inline `[Пропустить]` button

#### Scenario: User enters quantity
- **WHEN** the user replies with a quantity string during step 2
- **THEN** the bot advances to step 3 and asks "Срок годности? (например, 15.05.2026)" with an inline `[Пропустить]` button; the item is NOT saved yet

#### Scenario: User skips quantity
- **WHEN** the user taps `[Пропустить]` during step 2
- **THEN** the bot advances to step 3 and asks "Срок годности? (например, 15.05.2026)" with an inline `[Пропустить]` button; the item is NOT saved yet

#### Scenario: User enters expiry date
- **WHEN** the user replies with a valid date in `dd.MM.yyyy` format during step 3
- **THEN** the item is saved with that expiry date and the bot confirms "Иван добавил(а) Молоко 2л (до 15.05.2026)"

#### Scenario: User skips expiry date
- **WHEN** the user taps `[Пропустить]` during step 3
- **THEN** the item is saved with no expiry date and the bot confirms "Иван добавил(а) Молоко 2л"

#### Scenario: /buy used in private chat
- **WHEN** a user sends `/buy` in a private chat
- **THEN** the bot replies "Эта команда работает только в групповом чате." and does not start a dialog

---

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat showing all current items with inline action buttons. Items with an expiry date SHALL display the date; items expiring today or already expired SHALL be prefixed with ⚠️.

#### Scenario: List has items without expiry dates
- **WHEN** a group member sends `/list` and items have no `exp_date`
- **THEN** the bot posts (or edits the existing) list message with header "🛒 Список покупок:" and one inline button row per item: `[✓ Name qty]` and `[✗ Убрать]`

#### Scenario: List has items with expiry dates
- **WHEN** a group member sends `/list` and some items have `exp_date` set
- **THEN** items with an expiry date are shown as "• Name qty (до DD.MM)" in the text body, and their check button label is "✓ Name qty (до DD.MM)"

#### Scenario: List has expired or expiring-today items
- **WHEN** a group member sends `/list` and some items have `exp_date <= today`
- **THEN** those items are prefixed with ⚠️ in the text body: "⚠️ Name qty (до DD.MM)"

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
