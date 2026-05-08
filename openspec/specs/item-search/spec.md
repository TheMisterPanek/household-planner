## ADDED Requirements

### Requirement: /search command looks up purchase history by item name
The system SHALL register a `/search` command handler that accepts a query argument (e.g., `/search Milk`). It SHALL call `PurchaseHistoryRepository.SearchAsync` and format the results grouped by item name (case-insensitive). If no argument is provided, the bot SHALL reply with usage guidance.

#### Scenario: /search with matching results
- **WHEN** a user sends `/search Milk` and the group has purchase history for "Milk 2л"
- **THEN** the bot replies with a formatted list showing the item name, latest store, latest price, date, and buyer

#### Scenario: /search with no results
- **WHEN** a user sends `/search Milk` and no matching records exist for the group
- **THEN** the bot replies with "No purchase history found for 'Milk'"

#### Scenario: /search with no argument
- **WHEN** a user sends `/search` with no text after the command
- **THEN** the bot replies with usage guidance (e.g., "Usage: /search <item name>")

---

### Requirement: /search shows price dynamics when multiple purchases exist
For each item name group in search results, if two or more records have a non-null `Price`, the system SHALL compute and display a price delta: `round((latestPrice - previousPrice) / previousPrice * 100, 1)`, formatted as `+N.N%` or `-N.N%` appended to the latest price.

#### Scenario: Price increased since last purchase
- **WHEN** search results contain two purchases of "Milk" with prices 89.90 and 94.90 (newest first)
- **THEN** the output shows "94.90 (+5.6%)"

#### Scenario: Price decreased since last purchase
- **WHEN** search results contain two purchases of "Milk" with prices 94.90 and 89.90 (newest first)
- **THEN** the output shows "89.90 (-5.3%)"

#### Scenario: Price unchanged since last purchase
- **WHEN** search results contain two purchases of "Milk" both priced 89.90
- **THEN** the output shows "89.90 (0.0%)"

#### Scenario: Only one purchase record exists
- **WHEN** search results contain one purchase of "Milk" with price 89.90
- **THEN** the output shows "89.90" with no delta

#### Scenario: Latest purchase has no price
- **WHEN** the most recent purchase of an item has `Price = null`
- **THEN** no price or delta is shown for that item group

#### Scenario: Multiple distinct item names matched
- **WHEN** `/search Milk` matches "Milk 2л" and "Oat Milk"
- **THEN** each item name is shown as a separate group with its own latest-purchase line and delta (if applicable)
