## ADDED Requirements

### Requirement: PurchaseHistory table stores completed purchase records
The system SHALL create a `PurchaseHistory` table in SQLite via `DatabaseInitializer` with columns: `Id`, `GroupId`, `ItemName`, `Quantity` (nullable), `StoreName` (nullable), `Price` (nullable REAL), `PurchasedAt` (ISO 8601 TEXT, NOT NULL), `BoughtByName` (TEXT, NOT NULL).

#### Scenario: Table created on fresh database
- **WHEN** the application starts and `PurchaseHistory` does not exist
- **THEN** the table is created with all specified columns before any commands are processed

#### Scenario: Table creation is idempotent
- **WHEN** the application starts and `PurchaseHistory` already exists
- **THEN** no error occurs and existing records are preserved

---

### Requirement: Purchase records can be added and queried by group
The system SHALL provide a `PurchaseHistoryRepository` with `AddAsync(record)` and `SearchAsync(groupId, query)` methods. `SearchAsync` SHALL return all records for the group where `ItemName` contains the query string (case-insensitive), ordered by `PurchasedAt` descending.

#### Scenario: Record is saved with all fields
- **WHEN** `AddAsync` is called with a fully populated `PurchaseRecord`
- **THEN** a row is inserted and the returned record has a non-zero `Id`, with all fields matching the input

#### Scenario: Record is saved with optional fields null
- **WHEN** `AddAsync` is called with `StoreName = null` and `Price = null`
- **THEN** the row is inserted with NULL in those columns and no error occurs

#### Scenario: SearchAsync returns matching records for the group
- **WHEN** `SearchAsync(groupId, "Milk")` is called and the group has purchase history containing "Milk 2л" and "Oat Milk"
- **THEN** both records are returned, ordered by `PurchasedAt` descending

#### Scenario: SearchAsync returns no records for a different group
- **WHEN** `SearchAsync(groupId, "Milk")` is called but matching records belong to a different group
- **THEN** an empty list is returned

#### Scenario: SearchAsync is case-insensitive
- **WHEN** `SearchAsync(groupId, "milk")` is called and history contains "Milk"
- **THEN** the "Milk" record is returned

---

### Requirement: Price-capture dialog state is tracked per user per chat
The system SHALL use `PendingDialogService<PriceCaptureDialogState>` to persist in-progress price-capture sessions keyed by `(chatId, userId)`. The state SHALL hold: `Step` (1 = awaiting store, 2 = awaiting price), `ItemName`, `Quantity`, `BoughtByName`, and optionally `StoreName`.

#### Scenario: State is set when ✓ is tapped
- **WHEN** `ShopDoneCallbackHandler` processes a ✓ tap
- **THEN** a `PriceCaptureDialogState` with `Step = 1` is set for `(chatId, callbackQuery.From.Id)`

#### Scenario: State is cleared after dialog completion
- **WHEN** the user completes or skips both steps of the price-capture dialog
- **THEN** `PendingDialogService<PriceCaptureDialogState>.ClearState` is called for `(chatId, userId)`
