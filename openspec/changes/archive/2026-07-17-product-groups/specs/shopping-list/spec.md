## MODIFIED Requirements

### Requirement: Add item to shopping list via /buy command
The system SHALL accept `/buy` in a group chat. If arguments are provided inline, the item is added immediately; otherwise a two-step dialog is initiated. The inline input supports comma-separated items, each parsed as an independent item entry. After any of these paths successfully persists item(s), the system SHALL follow up with a category-capture prompt per the `product-tags` capability; this does not alter the existing item-added confirmation text or timing.

#### Scenario: User sends /buy with single name and quantity inline
- **WHEN** a group member sends `/buy Молоко 2л`
- **THEN** the item is saved immediately and the bot confirms "Иван добавил(а) Молоко 2л" (last token = quantity, everything before = name)

#### Scenario: User sends /buy with single name only inline
- **WHEN** a group member sends `/buy Молоко`
- **THEN** the item is saved immediately with no quantity and the bot confirms "Иван добавил(а) Молоко"

#### Scenario: User sends /buy with comma-separated items
- **WHEN** a group member sends `/buy Молоко, Яйца, Хлеб`
- **THEN** three items are saved immediately (all without quantity) and the bot confirms "Иван добавил(а) 3 товара: Молоко, Яйца, Хлеб"

#### Scenario: User sends /buy with comma-separated items with quantities
- **WHEN** a group member sends `/buy Молоко 2л, Яйца 6, Хлеб`
- **THEN** three items are saved: (Молоко, 2л), (Яйца, 6), (Хлеб, null) and the bot confirms "Иван добавил(а) 3 товара: Молоко 2л, Яйца 6, Хлеб"

#### Scenario: User sends /buy with whitespace around commas
- **WHEN** a group member sends `/buy Молоко  ,  Яйца  ,  Хлеб`
- **THEN** whitespace is trimmed from each segment and three items are saved without quantity, confirmed as "Иван добавил(а) 3 товара: Молоко, Яйца, Хлеб"

#### Scenario: User sends /buy with excessive comma-separated items
- **WHEN** a group member sends `/buy item1, item2, item3, ... item21` (21+ items)
- **THEN** only the first 20 items are added, the bot confirms "Иван добавил(а) 20 товаров: item1, item2, ..., item20" and logs a warning

#### Scenario: User completes full /buy dialog
- **WHEN** a group member sends `/buy` with no arguments
- **THEN** the bot replies "Что купить?" and waits for the next message from that user

#### Scenario: User enters item name in dialog
- **WHEN** the user replies with an item name after `/buy`
- **THEN** the bot asks "Сколько?" with an inline `[Пропустить]` button, unchanged from current behavior

#### Scenario: User enters quantity in dialog
- **WHEN** the user replies with a quantity string
- **THEN** the item is saved to the database and the bot confirms "Иван добавил(а) Молоко 2л"

#### Scenario: User skips quantity in dialog
- **WHEN** the user taps `[Пропустить]`
- **THEN** the item is saved with no quantity and the bot confirms "Иван добавил(а) Молоко"

#### Scenario: /buy used in private chat
- **WHEN** a user sends `/buy` in a private chat
- **THEN** the bot replies "Эта команда работает только в групповом чате." and does not start a dialog

#### Scenario: Category prompt follows a successful inline single add
- **WHEN** a group member sends `/buy Молоко 2л` and the bot confirms "Иван добавил(а) Молоко 2л"
- **THEN** the bot additionally sends a category-capture prompt for "Молоко 2л" per the `product-tags` capability, as a separate follow-up message

#### Scenario: Category prompt follows a successful bulk add, once for the batch
- **WHEN** a group member sends `/buy Молоко, Яйца, Хлеб` and the bot confirms "Иван добавил(а) 3 товара: Молоко, Яйца, Хлеб"
- **THEN** the bot additionally sends one category-capture prompt covering all three items, not one per item

#### Scenario: Category prompt follows the skip-quantity dialog path
- **WHEN** a user taps `[Пропустить]` in the `/buy` dialog and the item is saved with no quantity
- **THEN** the bot additionally sends a category-capture prompt for that item after the "Иван добавил(а) Молоко" confirmation

---

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat showing all current items with inline action buttons. When the group has at least one item with a non-null `Category`, the message SHALL include an additional row of category filter buttons (per the `product-tags` capability) below the item rows.

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
- **THEN** the list message renders exactly as before, with no category filter button row

#### Scenario: List with categorized items shows a filter row
- **WHEN** a group member sends `/list` and at least one item has a `Category` set
- **THEN** the list message includes a row of category filter buttons (up to 5, alphabetical) below the item rows, in addition to the unfiltered item list and existing action buttons

---

## ADDED Requirements

### Requirement: Filter the shopping list by category
The system SHALL let a user tap a category filter button on the `/list` message to re-render the message showing only items whose `Category` matches the tapped category, using the same pagination as the unfiltered view. A "Все" (All) button SHALL be shown when a filter is active, returning to the unfiltered view.

#### Scenario: User taps a category filter button
- **WHEN** a user taps the "Бытовая химия" filter button on the list message
- **THEN** the message is edited to show only items with `Category = "Бытовая химия"`, paginated the same way as the unfiltered list, with an "Все" button to clear the filter

#### Scenario: User taps "Все" to clear an active filter
- **WHEN** a user taps the "Все" button while a category filter is active
- **THEN** the message is edited back to the unfiltered, paginated view of all items

#### Scenario: Filtered view respects pagination for large categories
- **WHEN** a category filter is active and more than 10 items match that category
- **THEN** the filtered view paginates at 10 items per page, using the same next/prev navigation as the unfiltered list

#### Scenario: Category no longer exists at tap time
- **WHEN** a user taps a category filter button whose category no longer has any items (e.g. all were removed or re-tagged since the message was rendered)
- **THEN** the bot falls back to showing the unfiltered "All" view rather than an empty or error state
