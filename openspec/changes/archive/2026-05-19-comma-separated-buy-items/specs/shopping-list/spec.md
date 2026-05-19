## MODIFIED Requirements

### Requirement: Add item to shopping list via /buy command
The system SHALL accept `/buy` in a group chat. If arguments are provided inline, the item is added immediately; otherwise a two-step dialog is initiated. The inline input now supports comma-separated items, each parsed as an independent item entry.

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
- **THEN** the bot asks "Сколько?" with an inline `[Пропустить]` button

#### Scenario: User enters quantity in dialog
- **WHEN** the user replies with a quantity string
- **THEN** the item is saved to the database and the bot confirms "Иван добавил(а) Молоко 2л"

#### Scenario: User skips quantity in dialog
- **WHEN** the user taps `[Пропустить]`
- **THEN** the item is saved with no quantity and the bot confirms "Иван добавил(а) Молоко"

#### Scenario: /buy used in private chat
- **WHEN** a user sends `/buy` in a private chat
- **THEN** the bot replies "Эта команда работает только в групповом чате." and does not start a dialog

---

## ADDED Requirements

### Requirement: CSV bulk addition records history
The system SHALL record one `BotActionType.ItemAdded` history entry per item when bulk items are added via comma-separated `/buy` command. History recording failures SHALL not suppress the confirmation message.

#### Scenario: Bulk items added via comma-separated /buy records history for each item
- **WHEN** a group member sends `/buy Молоко, Яйца, Хлеб` and all three items are saved
- **THEN** three separate `ItemAdded` history entries are recorded for the chat, one per item

#### Scenario: History write failure does not suppress bulk confirmation
- **WHEN** `IHistoryRepository.RecordAsync` raises an exception during bulk `/buy` processing
- **THEN** the "Иван добавил(а) N товара" confirmation is still sent and errors are logged at Warning level
