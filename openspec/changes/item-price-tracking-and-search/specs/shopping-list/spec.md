## MODIFIED Requirements

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
