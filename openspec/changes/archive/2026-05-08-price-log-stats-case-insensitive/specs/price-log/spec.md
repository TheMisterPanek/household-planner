## ADDED Requirements

### Requirement: PriceLog table stores price observations per item
The system SHALL create a `PriceLog` table in SQLite via `DatabaseInitializer` with columns: `Id` (INTEGER PK), `GroupId` (INTEGER NOT NULL), `ItemName` (TEXT NOT NULL), `Price` (REAL NOT NULL), `StoreName` (TEXT, nullable), `LoggedAt` (TEXT NOT NULL, ISO-8601 UTC).

#### Scenario: Table created on fresh database
- **WHEN** the application starts and `PriceLog` does not exist
- **THEN** the table is created with all specified columns before any commands are processed

#### Scenario: Table creation is idempotent
- **WHEN** the application starts and `PriceLog` already exists
- **THEN** no error occurs and existing records are preserved

---

### Requirement: Price observations can be added to the log
The system SHALL provide a `PriceLogRepository` with an `AddAsync(groupId, itemName, price, storeName, loggedAt)` method that inserts a row into `PriceLog` and returns the saved record with its assigned `Id`.

#### Scenario: Record is saved with all fields
- **WHEN** `AddAsync` is called with groupId, itemName "Milk", price 2.50, storeName "Carrefour", and a UTC timestamp
- **THEN** a row is inserted and the returned record has a non-zero `Id` with all fields matching the input

#### Scenario: Record is saved with null store name
- **WHEN** `AddAsync` is called with `storeName = null`
- **THEN** the row is inserted with NULL in `StoreName` and no error occurs

---

### Requirement: Price stats can be queried per item per group (case-insensitive)
The system SHALL provide a `GetStatsAsync(groupId, itemName)` method on `PriceLogRepository` that returns a `PriceStats` record containing `Min`, `Avg`, `Max` (all `decimal`), `Count` (int), and a `StoreBreakdown` (up to 10 stores with their individual min/avg/max), matching item names case-insensitively via `LOWER()`.

#### Scenario: Stats returned for existing item
- **WHEN** `GetStatsAsync(groupId: 1, "Milk")` is called and there are 3 records with prices 1.50, 2.00, 2.50
- **THEN** `Min = 1.50`, `Avg = 2.00`, `Max = 2.50`, `Count = 3` are returned

#### Scenario: Stats match case-insensitively
- **WHEN** `GetStatsAsync(groupId: 1, "milk")` is called and records use item name "Milk"
- **THEN** the matching records are found and stats are returned (not null / empty)

#### Scenario: Stats are null when no records exist
- **WHEN** `GetStatsAsync(groupId: 1, "Unknown item")` is called and no records exist for that item
- **THEN** the method returns `null`

#### Scenario: Stats are scoped to the group
- **WHEN** `GetStatsAsync(groupId: 1, "Milk")` is called and matching records exist only in groupId 2
- **THEN** the method returns `null`

#### Scenario: Store breakdown is included
- **WHEN** records for "Milk" include 2 entries at "Carrefour" (prices 1.80, 2.20) and 1 entry at "Lidl" (price 1.50)
- **THEN** `StoreBreakdown` contains entries for "Carrefour" (min 1.80, avg 2.00, max 2.20) and "Lidl" (min/avg/max 1.50)

#### Scenario: Store breakdown is capped at 10 stores
- **WHEN** an item has price records across 15 distinct stores
- **THEN** `StoreBreakdown` contains exactly 10 entries (the 10 stores with the most observations)

#### Scenario: Cyrillic item name matches case-insensitively
- **WHEN** `GetStatsAsync(groupId: 1, "молоко")` is called and records use item name "Молоко"
- **THEN** the matching records are found and stats are returned

---

### Requirement: Price observation is recorded when price capture completes with a non-null price
The system SHALL call `PriceLogRepository.AddAsync` after a successful price-capture dialog completion where the user provided a non-null price. The call SHALL be wrapped in try/catch — a failure logs at Warning and does not suppress the user-facing confirmation.

#### Scenario: Price log entry written on price capture completion
- **WHEN** the price-capture dialog completes with `Price = 2.50` and `StoreName = "Carrefour"`
- **THEN** a `PriceLog` entry is inserted with the item name, price, store name, and current UTC timestamp

#### Scenario: Price log not written when price is skipped
- **WHEN** the price-capture dialog completes with `Price = null` (user skipped)
- **THEN** no row is inserted into `PriceLog`

#### Scenario: Price log write failure does not suppress confirmation
- **WHEN** `PriceLogRepository.AddAsync` throws an exception
- **THEN** the user-facing price-capture confirmation is still sent and the error is logged at Warning
