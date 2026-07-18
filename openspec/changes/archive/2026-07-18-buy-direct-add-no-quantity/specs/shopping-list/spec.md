## MODIFIED Requirements

### Requirement: Add item to shopping list via /buy command
The system SHALL accept `/buy` in a group chat. If arguments are provided inline, the item is added immediately when the input is a comma-separated bulk list, or when no quantity was detected in the input; otherwise (a single item where a quantity was detected) a review step is shown before saving. If no arguments are provided, a two-step dialog is initiated. The inline input supports comma-separated items, each parsed as an independent item entry.

#### Scenario: User sends /buy with single name and quantity inline
- **WHEN** a group member sends `/buy Молоко 2л`
- **THEN** a review message "Добавить: Молоко 2л?" is shown with Confirm/✏️ Изменить/Cancel buttons, and the item is only saved once Confirm is tapped (last token = quantity, everything before = name)

#### Scenario: User sends /buy with single name only inline
- **WHEN** a group member sends `/buy Молоко`
- **THEN** no quantity is detected, so the item is saved immediately with no review step, and the bot confirms "Иван добавил(а) Молоко"

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

#### Scenario: No-quantity inline add records history and starts category capture
- **WHEN** a group member sends `/buy Молоко` and the item is saved immediately (no review step)
- **THEN** an `ItemAdded` history entry is recorded and the category-capture follow-up prompt is started for that item, exactly as it would be after tapping Confirm on a reviewed add

#### Scenario: Quantity-detected inline add is unaffected
- **WHEN** a group member sends `/buy Молоко 2л`
- **THEN** the review step (Confirm/✏️ Изменить/Cancel) still appears exactly as before this change, and the item is not saved until Confirm is tapped
