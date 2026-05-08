## Purpose

Track purchase history and enable shopping experience enhancements through historical purchase data.
## Requirements
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

### Requirement: Query top shops by frequency for a user in a group
The system SHALL provide a `GetTopShopsAsync(int groupId, long userId, int limit)` method on `PurchaseHistoryRepository` that retrieves the user's most frequently visited shops in a group, sorted by frequency (descending) and recent visit date (descending on ties).

#### Scenario: Get top 5 shops
- **WHEN** `GetTopShopsAsync(groupId: 10, userId: 123, limit: 5)` is called and the user has purchase history with 3 distinct shop names
- **THEN** the method returns `IReadOnlyList<string>` with 3 shop names in frequency order (most frequent first)

#### Scenario: Exclude null shop names
- **WHEN** querying top shops for a user whose history contains purchases with `StoreName = null` and some with non-null values
- **THEN** null values are excluded from the result; only non-null shop names are returned

#### Scenario: Order by frequency then recency
- **WHEN** the user has 2 purchases from "Shop A" (oldest first, newest 2026-05-01) and 2 purchases from "Shop B" (oldest first, newest 2026-05-08)
- **THEN** both shops have frequency=2, but "Shop B" appears first due to more recent `PurchasedAt`

#### Scenario: Limit result set
- **WHEN** `GetTopShopsAsync(..., limit: 5)` is called and the user has 10 distinct shops
- **THEN** the method returns at most 5 shop names; additional shops are discarded

#### Scenario: User has no purchase history
- **WHEN** `GetTopShopsAsync(groupId: 10, userId: 999)` is called for a user with no records in `PurchaseHistory`
- **THEN** an empty list is returned

