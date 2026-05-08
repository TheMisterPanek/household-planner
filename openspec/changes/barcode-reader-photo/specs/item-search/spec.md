## ADDED Requirements

### Requirement: Item-search flow accepts a pre-filled term from barcode lookup
The system SHALL expose an entry point in the item-search flow that accepts an optional pre-filled search term. When a barcode lookup resolves to a product name and the user taps the "Add as item" inline button, the `barcode_add` callback handler SHALL invoke this entry point with the product name as the pre-filled term, bypassing the initial "type item name" prompt. All subsequent item-search behavior (results display, selection, adding to shopping list) SHALL remain unchanged.

#### Scenario: Pre-filled term launches item-search immediately
- **WHEN** the user taps "Add as item" after a successful barcode lookup returning product "Coca-Cola"
- **THEN** the bot immediately shows item-search results for "Coca-Cola" without asking the user to type anything

#### Scenario: Pre-filled term finds no results
- **WHEN** the pre-filled product name returns no item-search matches
- **THEN** the bot replies with the existing item-search "no results" message, consistent with a manual search producing no results

#### Scenario: Pre-filled term from raw barcode
- **WHEN** the user taps "Use barcode as item name" after a not-found lookup (raw barcode e.g. `9999999999999`)
- **THEN** the bot runs item-search pre-filled with `9999999999999` and displays results or the no-results reply
