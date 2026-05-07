## MODIFIED Requirements

### Requirement: Mark shopping item as bought
The system SHALL remove the item from the shopping list and confirm when a user taps the check button.

#### Scenario: User marks item bought
- **WHEN** a user taps `[✓ Name qty]` on a list item
- **THEN** the item is deleted from the database, the list message is updated, and the bot sends "Иван: Молоко 2л — отмечено ✓"

#### Scenario: Item already deleted (stale callback)
- **WHEN** a user taps `[✓ Name qty]` but the item no longer exists in the database
- **THEN** the list message is refreshed to its current state and no error is shown to the user

---

### Requirement: Remove shopping item without marking bought
The system SHALL delete the item from the list when the user taps the remove button.

#### Scenario: User removes item
- **WHEN** a user taps `[✗ Убрать]` on a list item
- **THEN** the item is deleted from the database and the list message is updated immediately

#### Scenario: Item already deleted (stale callback)
- **WHEN** a user taps `[✗ Убрать]` but the item no longer exists in the database
- **THEN** the list message is refreshed to its current state and no error is shown to the user
