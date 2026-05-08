### Requirement: Add item to shopping list via /buy command
The system SHALL accept `/buy` in a group chat. If arguments are provided inline, the item is added immediately; otherwise a two-step dialog is initiated.

#### Scenario: User sends /buy with name and quantity inline
- **WHEN** a group member sends `/buy Молоко 2л`
- **THEN** the item is saved immediately and the bot confirms "Иван добавил(а) Молоко 2л" (last token = quantity, everything before = name)

#### Scenario: User sends /buy with name only inline
- **WHEN** a group member sends `/buy Молоко`
- **THEN** the item is saved immediately with no quantity and the bot confirms "Иван добавил(а) Молоко"

#### Scenario: User completes full /buy dialog
- **WHEN** a group member sends `/buy` with no arguments
- **THEN** the bot replies "Что купить?" and waits for the next message from that user

#### Scenario: User enters item name
- **WHEN** the user replies with an item name after `/buy`
- **THEN** the bot asks "Сколько?" with an inline `[Пропустить]` button

#### Scenario: User enters quantity
- **WHEN** the user replies with a quantity string
- **THEN** the item is saved to the database and the bot confirms "Иван добавил(а) Молоко 2л"

#### Scenario: User skips quantity
- **WHEN** the user taps `[Пропустить]`
- **THEN** the item is saved with no quantity and the bot confirms "Иван добавил(а) Молоко"

#### Scenario: /buy used in private chat
- **WHEN** a user sends `/buy` in a private chat
- **THEN** the bot replies "Эта команда работает только в групповом чате." and does not start a dialog

---

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat showing all current items with inline action buttons.

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

---

### Requirement: Mark shopping item as bought
When a user taps the ✓ button on a list item, the system SHALL immediately remove the item from the active shopping list and update the list message, then initiate a 2-step optional price-capture dialog with the user who tapped the button. The dialog SHALL allow skipping each step individually.

#### Scenario: User marks item bought — store step
- **WHEN** a user taps `[✓ Name qty]` on a list item
- **THEN** the item is deleted from `ShoppingItems`, the list message is updated, a `PriceCaptureDialogState` is set for `(chatId, userId)` at step 1, and the bot sends "📍 Where did you buy {Name}?" with a [Skip] inline button

#### Scenario: User provides store name
- **WHEN** the user replies with a text message while in price-capture step 1
- **THEN** `StoreName` is saved to dialog state, step advances to 2, and the bot asks "💰 Price for {Name}?" with a [Skip] inline button

#### Scenario: User skips store step
- **WHEN** the user taps [Skip] during price-capture step 1 (callback `price:skip_store`)
- **THEN** `StoreName` is left null, step advances to 2, and the bot asks "💰 Price for {Name}?" with a [Skip] inline button

#### Scenario: User provides price
- **WHEN** the user replies with a valid numeric string during price-capture step 2
- **THEN** the price is parsed as a decimal, a `PurchaseRecord` is saved to `PurchaseHistory`, the dialog is cleared, and the bot sends a confirmation including store and price if provided

#### Scenario: User provides invalid price
- **WHEN** the user replies with a non-numeric string during price-capture step 2
- **THEN** the bot replies with "That doesn't look like a price — try again or tap [Skip]" and remains on step 2

#### Scenario: User skips price step
- **WHEN** the user taps [Skip] during price-capture step 2 (callback `price:skip_price`)
- **THEN** a `PurchaseRecord` is saved with `Price = null`, the dialog is cleared, and the bot sends a confirmation

#### Scenario: User abandons price-capture dialog
- **WHEN** the user taps ✓ on another item while already in a price-capture dialog
- **THEN** the previous dialog state is discarded, the new item's dialog is started from step 1, and the old item remains in history without a price record (as none was saved)

---

### Requirement: Remove shopping item without marking bought
The system SHALL delete the item from the list when the user taps the remove button. This path is unchanged — no price-capture dialog is initiated.

#### Scenario: User removes item
- **WHEN** a user taps `[✗ Убрать]` on a list item
- **THEN** the item is deleted from `ShoppingItems` and the list message is updated immediately with no price dialog

---

### Requirement: Group auto-registration on first command
The system SHALL create a `Group` database record on the first command received in a new group chat.

#### Scenario: First command in a new group
- **WHEN** any command is received in a group chat with no existing `Group` record
- **THEN** a new `Group` row is inserted with the `ChatId` before the command is processed

#### Scenario: Subsequent commands in existing group
- **WHEN** a command is received in a group chat that already has a `Group` record
- **THEN** no duplicate insert occurs

---

### Requirement: /buy handlers record ItemAdded history entry
The system SHALL record a `BotActionType.ItemAdded` history entry via `IHistoryRepository.RecordAsync` after an item is successfully saved to the database by any `/buy` flow (inline, dialog, or skip-quantity). The payload SHALL include the item name and optional quantity.

#### Scenario: Item added via inline /buy command records history
- **WHEN** a group member sends `/buy Молоко 2л` and the item is saved
- **THEN** an `ItemAdded` history entry is recorded for the chat with the item name and quantity in the payload

#### Scenario: Item added via dialog completion records history
- **WHEN** a user completes the `/buy` dialog (with or without quantity) and the item is saved
- **THEN** an `ItemAdded` history entry is recorded for the chat

#### Scenario: History write failure does not suppress confirmation message
- **WHEN** `IHistoryRepository.RecordAsync` raises an exception during a `/buy` flow
- **THEN** the "Иван добавил(а) Молоко" confirmation is still sent and the error is logged at Warning level

---

### Requirement: /list command records ListViewed history entry
The system SHALL record a `BotActionType.ListViewed` history entry via `IHistoryRepository.RecordAsync` after the list message is successfully posted or edited by `/list`.

#### Scenario: List viewed records history
- **WHEN** a group member sends `/list` and the bot posts or edits the list message successfully
- **THEN** a `ListViewed` history entry is recorded for the chat

#### Scenario: History write failure does not suppress list message
- **WHEN** `IHistoryRepository.RecordAsync` raises an exception during `/list`
- **THEN** the list message is still posted or edited and the error is logged at Warning level

---

### Requirement: Buy (done) callback records ItemBought history entry
The system SHALL record a `BotActionType.ItemBought` history entry via `IHistoryRepository.RecordAsync` after an item is deleted from the database by the `[✓]` button callback. The payload SHALL include the item name and optional quantity.

#### Scenario: Item marked bought records history
- **WHEN** a user taps `[✓ Name qty]` and the item is deleted
- **THEN** an `ItemBought` history entry is recorded for the chat with the item name and quantity

#### Scenario: History write failure does not suppress done-action response
- **WHEN** `IHistoryRepository.RecordAsync` raises an exception during the buy callback
- **THEN** the list message is still updated and the error is logged at Warning level

---

### Requirement: Remove callback records ItemRemoved history entry
The system SHALL record a `BotActionType.ItemRemoved` history entry via `IHistoryRepository.RecordAsync` after an item is deleted from the database by the `[✗ Убрать]` button callback. The payload SHALL include the item name.

#### Scenario: Item removed records history
- **WHEN** a user taps `[✗ Убрать]` and the item is deleted
- **THEN** an `ItemRemoved` history entry is recorded for the chat with the item name

#### Scenario: History write failure does not suppress remove response
- **WHEN** `IHistoryRepository.RecordAsync` raises an exception during the remove callback
- **THEN** the list message is still updated and the error is logged at Warning level
