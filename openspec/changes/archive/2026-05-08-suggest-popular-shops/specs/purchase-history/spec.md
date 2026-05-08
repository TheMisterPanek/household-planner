## ADDED Requirements

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
