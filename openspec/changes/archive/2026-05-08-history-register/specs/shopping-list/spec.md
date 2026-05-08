## ADDED Requirements

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
