## ADDED Requirements

### Requirement: Add day's ingredients to shopping list
When the user taps the "Add ingredients to cart" button in the day detail view, the system SHALL collect all ingredients from meals assigned to that day and add non-duplicate items to the shopping list.

#### Scenario: Add ingredients from day's meals
- **GIVEN** Monday has "Pasta" (ingredients: Flour 500g, Eggs 3) and "Soup" (ingredients: Broth 1L)
- **WHEN** user taps [🛒 Add ingredients to cart] on Monday
- **THEN** Flour, Eggs, and Broth are added to the shopping list with addedByName "Week"
- **AND** bot answers callback with "✓ Added 3 items to shopping list"

#### Scenario: Skip duplicates
- **GIVEN** Monday has "Pasta" (ingredient: Flour 500g) and "Flour" already exists on the shopping list
- **WHEN** user taps [🛒 Add ingredients to cart]
- **THEN** Flour is NOT added again
- **AND** bot answers callback with "✓ Added 0 items to shopping list"

#### Scenario: No meals assigned
- **WHEN** user taps [🛒 Add ingredients to cart] on a day with no meals
- **THEN** bot answers callback with localized "week.cart-no-meals" toast

#### Scenario: Meals have no ingredients
- **GIVEN** Monday has "Pasta" but Pasta has no ingredients defined
- **WHEN** user taps [🛒 Add ingredients to cart]
- **THEN** bot answers callback with localized "week.cart-no-ingredients" toast

### Requirement: Case-insensitive deduplication
The system SHALL check for existing shopping list items using case-insensitive name comparison.

#### Scenario: Case-insensitive match
- **GIVEN** shopping list has "flour" and meal ingredient is "Flour"
- **WHEN** user taps [🛒 Add ingredients to cart]
- **THEN** "Flour" is NOT added (treated as duplicate)
