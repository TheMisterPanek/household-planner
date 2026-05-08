## ADDED Requirements

### Requirement: /prices command shows price statistics for a named item
The system SHALL accept `/prices <item name>` in a group chat and reply with a formatted message showing min, avg, and max price for the item (case-insensitive lookup), count of observations, and a per-store breakdown (up to 10 stores). The command SHALL be group-only.

#### Scenario: Stats found for item
- **WHEN** a group member sends `/prices Milk` and there are price log entries for "Milk" in the group
- **THEN** the bot replies with a message showing the item name, min/avg/max prices, total count, and a per-store breakdown

#### Scenario: Stats found with Cyrillic item name
- **WHEN** a group member sends `/prices молоко` and records exist with item name "Молоко"
- **THEN** the bot replies with stats for "Молоко" (case-insensitive match)

#### Scenario: No price data found for item
- **WHEN** a group member sends `/prices Tofu` and no `PriceLog` entries exist for "Tofu" in the group
- **THEN** the bot replies with a localized "no price data found" message (key: `prices.not-found`)

#### Scenario: /prices used without an argument
- **WHEN** a group member sends `/prices` with no argument
- **THEN** the bot replies with a localized usage hint (key: `prices.usage`)

#### Scenario: /prices used in private chat
- **WHEN** a user sends `/prices Milk` in a private chat
- **THEN** the bot replies with the standard localized "group chat only" message and does not query the database

---

### Requirement: /prices reply format includes min, avg, max, and store breakdown
The formatted reply SHALL use localized keys for all labels. Prices SHALL be formatted to 2 decimal places. The per-store breakdown SHALL list each store on a separate line with its own min/avg/max. Stores with null `StoreName` SHALL be grouped under a localized "Unknown store" label.

#### Scenario: Reply includes all stat fields
- **WHEN** `/prices Milk` returns stats with min=1.50, avg=2.00, max=2.50, count=4
- **THEN** the reply contains all four values formatted to 2 decimal places

#### Scenario: Null store is labeled as unknown
- **WHEN** price entries include records with `StoreName = null`
- **THEN** the breakdown shows those entries under a localized "Unknown store" label (key: `prices.unknown-store`)

#### Scenario: Store breakdown is omitted when all entries have null store
- **WHEN** all price entries for the item have `StoreName = null`
- **THEN** the reply shows overall min/avg/max only, with a single "Unknown store" entry; no empty breakdown section is shown
