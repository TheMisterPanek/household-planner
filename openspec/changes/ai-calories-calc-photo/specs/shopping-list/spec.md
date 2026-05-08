## ADDED Requirements

### Requirement: Shopping items can include nutrition source metadata
The system SHALL allow shopping list items to be augmented with source information indicating whether they were added manually or from a photo analysis, along with optional calorie estimates.

#### Scenario: Item added manually via /buy shows no nutrition metadata
- **WHEN** a user adds an item via `/buy` command
- **THEN** the item is stored with source = "manual" and no nutrition data

#### Scenario: Item added from photo analysis includes calorie estimate
- **WHEN** a user adds an item from photo analysis to the shopping list
- **THEN** the item is stored with source = "photo-analysis", estimated_calories field populated, and a reference to the `NutritionAnalysis` record

#### Scenario: Shopping list displays nutrition data when available
- **WHEN** `/list` is called and some items have nutrition data from photo analysis
- **THEN** items with calorie estimates can optionally be displayed with a calorie indicator (e.g., "🍎 Яблоки 2шт (52 kcal)" where available); items without nutrition data show no indicator

#### Scenario: Item price capture works with photo-sourced items
- **WHEN** user marks a photo-sourced item as bought via `[✓]` button
- **THEN** the price-capture dialog proceeds identically to manual items, regardless of nutrition source

---

### Requirement: Nutrition data is preserved in history audit
The system SHALL record nutrition source and calorie data in history entries when items are added or bought.

#### Scenario: ItemAdded history entry from photo includes nutrition info
- **WHEN** an item is added to the shopping list from photo analysis
- **THEN** the `ItemAdded` history entry payload includes `source: "photo-analysis"` and `estimated_calories` field if available

#### Scenario: ItemBought history entry preserves nutrition data
- **WHEN** a user marks a photo-sourced item as bought
- **THEN** the `ItemBought` history entry includes the original nutrition source and estimated calories for audit trail
