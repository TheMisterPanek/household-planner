## Purpose

Manage the group's shared shopping list: add items via `/buy` (inline, bulk, or dialog), view and page through the list via `/list`, mark items as bought, and remove items.
## Requirements
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

---

### Requirement: CSV bulk addition records history
The system SHALL record one `BotActionType.ItemAdded` history entry per item when bulk items are added via comma-separated `/buy` command. History recording failures SHALL not suppress the confirmation message.

#### Scenario: Bulk items added via comma-separated /buy records history for each item
- **WHEN** a group member sends `/buy Молоко, Яйца, Хлеб` and all three items are saved
- **THEN** three separate `ItemAdded` history entries are recorded for the chat, one per item

#### Scenario: History write failure does not suppress bulk confirmation
- **WHEN** `IHistoryRepository.RecordAsync` raises an exception during bulk `/buy` processing
- **THEN** the "Иван добавил(а) N товара" confirmation is still sent and errors are logged at Warning level

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

