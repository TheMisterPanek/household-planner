## ADDED Requirements

### Requirement: Store optional expiry date on a shopping item
The system SHALL persist an optional expiry date (`DateOnly?`) on each `ShoppingItem`, stored as an ISO-8601 TEXT column `exp_date` in the `ShoppingItems` table. The column is nullable; existing rows without an expiry date SHALL remain unaffected.

#### Scenario: Database column added on startup for existing database
- **WHEN** the application starts and the `ShoppingItems` table does not have an `exp_date` column
- **THEN** `DatabaseInitializer` runs `ALTER TABLE ShoppingItems ADD COLUMN exp_date TEXT` and the column is present with NULL values for existing rows

#### Scenario: Database column already exists on startup
- **WHEN** the application starts and the `exp_date` column already exists
- **THEN** no migration runs and startup completes without error

---

### Requirement: Collect expiry date during /buy dialog (step 3)
After the quantity step (step 2), the system SHALL prompt the user for an expiry date before saving the item. The step SHALL be skippable.

#### Scenario: User enters a valid expiry date
- **WHEN** the user replies with a date string in `dd.MM.yyyy` format (e.g. "15.05.2026") during step 3
- **THEN** the item is saved with `exp_date` set to that date and the bot confirms "Иван добавил(а) Молоко 2л (до 15.05.2026)"

#### Scenario: User skips the expiry date
- **WHEN** the user taps `[Пропустить]` during step 3
- **THEN** the item is saved with `exp_date = NULL` and the bot confirms "Иван добавил(а) Молоко" (no date suffix)

#### Scenario: User enters an invalid date format
- **WHEN** the user sends text that cannot be parsed as `dd.MM.yyyy` during step 3
- **THEN** the bot replies "Неверный формат. Введите дату в формате ДД.ММ.ГГГГ или нажмите Пропустить." and remains at step 3

#### Scenario: User enters a date in the past
- **WHEN** the user sends a valid `dd.MM.yyyy` date that is earlier than today during step 3
- **THEN** the item is saved with that `exp_date` (no rejection — the user may be logging an already-purchased item)

#### Scenario: Step 3 reached after entering quantity (step 2)
- **WHEN** the user enters a quantity string during step 2
- **THEN** the bot advances to step 3 and asks "Срок годности? (например, 15.05.2026)" with an inline `[Пропустить]` button, without saving the item yet

#### Scenario: Step 3 reached after skipping quantity
- **WHEN** the user taps `[Пропустить]` during step 2
- **THEN** the bot advances to step 3 and asks "Срок годности? (например, 15.05.2026)" with an inline `[Пропустить]` button, without saving the item yet

---

### Requirement: Inline /buy with name only does not prompt for expiry
The system SHALL NOT add an expiry-date step for inline `/buy` invocations (name-only or name+quantity on the same command line). Expiry is only collected via the interactive dialog.

#### Scenario: Inline /buy saves item immediately without expiry
- **WHEN** a group member sends `/buy Молоко 2л`
- **THEN** the item is saved immediately with `exp_date = NULL` and the bot confirms without a date suffix
