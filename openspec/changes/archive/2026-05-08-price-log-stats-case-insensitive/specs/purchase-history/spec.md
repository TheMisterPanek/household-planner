## MODIFIED Requirements

### Requirement: Purchase records can be added and queried by group
The system SHALL provide a `PurchaseHistoryRepository` with `AddAsync(record)` and `SearchAsync(groupId, query)` methods. `SearchAsync` SHALL return all records for the group where `ItemName` contains the query string (case-insensitive), ordered by `PurchasedAt` descending. Case-insensitive matching SHALL use `LOWER(ItemName) LIKE LOWER('%' || @query || '%')` in SQL to correctly handle non-ASCII characters including Cyrillic and Polish diacritics.

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

#### Scenario: SearchAsync is case-insensitive for ASCII
- **WHEN** `SearchAsync(groupId, "milk")` is called and history contains "Milk"
- **THEN** the "Milk" record is returned

#### Scenario: SearchAsync is case-insensitive for Cyrillic
- **WHEN** `SearchAsync(groupId, "молоко")` is called and history contains "Молоко"
- **THEN** the "Молоко" record is returned

#### Scenario: SearchAsync is case-insensitive for Polish diacritics
- **WHEN** `SearchAsync(groupId, "żółty")` is called and history contains "Żółty"
- **THEN** the "Żółty" record is returned
