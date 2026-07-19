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
The system SHALL post or edit a persistent shopping list message in the group chat showing all current items with inline action buttons. When the group has at least one item carrying one or more tags, the message SHALL include additional rows of tag filter buttons, wrapped to at most 2 buttons per row. The item-pagination row and the tag-pagination row (when present) SHALL each render an inline page indicator button (`[n/N]`) between the Previous/Next buttons.

#### Scenario: List has items
- **WHEN** a group member sends `/list`
- **THEN** the bot posts (or edits the existing) list message with header "🛒 Список покупок:" and one inline button row per item: `[✓ Name qty]` and `[✕]`

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

#### Scenario: List with no tagged items shows no filter row
- **WHEN** a group member sends `/list` and no item in the group carries any tag
- **THEN** the list message renders with no tag filter button rows

#### Scenario: List with tagged items shows wrapped filter rows
- **WHEN** a group member sends `/list` and at least one item carries a tag, with 5 tags plus "All Items" active (6 buttons total)
- **THEN** the filter buttons render across 3 rows of at most 2 buttons each, rather than one crowded row

#### Scenario: Filter row paginates when there are more than 6 tags
- **WHEN** a group has 10 distinct tags
- **THEN** the filter block shows tag page 1 (tags 1-6, wrapped 2 per row) followed by a `[1/2] [Next →]` row; tags 7-10 are reachable by paging

#### Scenario: Tag page navigation preserves the active filter and item page
- **WHEN** a user on tag page 1 taps `[Next →]` in the tag-pagination row
- **THEN** the bot re-renders the list showing tag page 2, with the current item page and any active tag filter selection unchanged

#### Scenario: Toggling a tag preserves the current tag page
- **WHEN** a user on tag page 2 taps a tag button to add/remove it from the active filter
- **THEN** the bot re-renders with the updated filter applied and the tag block still showing tag page 2, not reset to tag page 1

#### Scenario: Item pagination resets the tag page
- **WHEN** a user taps `[← Previous]`/`[Next →]` on the item-action pagination row while the tag block is showing tag page 2
- **THEN** the bot re-renders the requested item page with the tag block reset to tag page 1

#### Scenario: All Items and Cancel remain reachable from any tag page
- **WHEN** a filter is active and the tag block is showing any tag page (not just the last one)
- **THEN** the "All Items" (clear filter) button and the "Cancel" button render as fixed rows immediately below the tag-page block, regardless of which tag page is displayed

#### Scenario: Item pagination row shows a page indicator
- **WHEN** the list has more than one carousel page of items
- **THEN** the pagination row renders `[← Previous] [n/N] [Next →]`, omitting Previous on the first page and Next on the last page

#### Scenario: No pagination row when there is only one page
- **WHEN** the list has only one carousel page (item count ≤ `ActionPageSize`)
- **THEN** no pagination row is rendered — only the item action rows, any filter rows, and the final Cancel row

#### Scenario: Tapping the page indicator does nothing
- **WHEN** a user taps the `[n/N]` page-indicator button (item or tag pagination) on the list message
- **THEN** the callback query is answered (no loading spinner persists) and the list message, item state, and any pending dialogs are left completely unchanged

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
- **WHEN** a user taps `[✕]` on a list item
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
The system SHALL record a `BotActionType.ItemRemoved` history entry via `IHistoryRepository.RecordAsync` after an item is deleted from the database by the `[✕]` button callback. The payload SHALL include the item name.

#### Scenario: Item removed records history
- **WHEN** a user taps `[✕]` and the item is deleted
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

### Requirement: Filter the shopping list by one or more tags
The system SHALL let a user tap one or more tag filter buttons on the `/list` message to re-render the message showing only items carrying at least one of the currently-active tags (OR semantics), using the same pagination as the unfiltered view. Tapping an active tag button again deactivates it. A "Все" (All) button SHALL be shown when any filter is active, clearing all active tags and returning to the unfiltered view.

#### Scenario: User taps a tag filter button
- **WHEN** a user taps the "Бытовая химия" filter button on the list message
- **THEN** the message is edited to show only items carrying the "Бытовая химия" tag, paginated the same way as the unfiltered list, with "Бытовая химия" now shown as active and an "Все" button available to clear the filter

#### Scenario: User taps a second tag filter button while one is already active
- **WHEN** "Бытовая химия" is already active and the user taps the "Скидка" filter button
- **THEN** the message is edited to show items carrying "Бытовая химия" OR "Скидка" (union, not intersection), with both buttons now shown as active

#### Scenario: User taps an already-active tag filter button to deactivate it
- **WHEN** "Бытовая химия" and "Скидка" are both active and the user taps "Бытовая химия" again
- **THEN** "Бытовая химия" is deactivated, and the message is edited to show only items carrying "Скидка"

#### Scenario: User taps "Все" to clear all active filters
- **WHEN** one or more tag filters are active and the user taps the "Все" button
- **THEN** the message is edited back to the unfiltered, paginated view of all items, and no tag buttons remain marked active

#### Scenario: Filtered view respects pagination for large tag sets
- **WHEN** one or more tag filters are active and more than 10 items match the active set
- **THEN** the filtered view paginates at 10 items per page, using the same next/prev navigation as the unfiltered list

#### Scenario: An active tag no longer exists at tap time
- **WHEN** a user taps "Все" or navigates pagination while one of the previously active tags no longer has any items (e.g. all were removed or re-tagged since the message was rendered)
- **THEN** the bot drops the stale tag from the active set and falls back to the unfiltered "All" view if no active tags remain, rather than showing an empty or error state

### Requirement: Item rows expose no edit control
The system SHALL render each `/list` item row with exactly two buttons — `[✓ Name qty]` (mark bought) and `[✕]` (remove) — and SHALL NOT include any in-place rename/requantify control. Changing an item's name or quantity requires removing it and re-adding it via `/buy`.

#### Scenario: Item row has exactly two buttons
- **WHEN** a group member sends `/list`
- **THEN** each item row contains exactly two inline buttons: `[✓ Name qty]` and `[✕]`, with no third "edit" button

#### Scenario: No callback exists for in-place item editing
- **WHEN** any callback data matching a legacy `item:edit:*` pattern is received (e.g. from a stale cached keyboard on an old list message)
- **THEN** `UpdateDispatcher` finds no matching `ICallbackHandler` prefix, logs it at Debug level, and takes no further action — no dialog opens, no error is shown to the user

