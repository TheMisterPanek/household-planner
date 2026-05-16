## Why

The original `/week` command showed all 7 days with inline meal details in a single flat view, making it cluttered and hard to navigate. Users needed a cleaner two-level UI: day buttons first, then detail view per day. Additionally, the schema enforced a single meal per day via UNIQUE constraint, but users need multiple meals per day (breakfast, lunch, dinner). Finally, users want to quickly add a day's meal ingredients to the shopping list directly from the week view.

## What Changes

- **Rework `/week` UI to two-level navigation**: Main view shows 7 day buttons only. Tapping a day shows its meals in a bullet list with per-meal clear buttons, add-meal button, back button, and add-to-cart button.
- **Allow multiple meals per day (up to 10)**: Remove UNIQUE(GroupId, DayOfWeek) constraint from DayMeals table. Add migration for existing databases.
- **Add "Add ingredients to cart" action**: From the day detail view, users can add that day's meal ingredients to the shopping list (deduped against existing items).
- **Show bullet list of meals in day detail view**: Message text displays meals as `• MealName` format, matching the `/list` command style.
- **Increase OpenRouter HTTP client timeout**: From 30s to 2min to handle longer AI queries.

## Capabilities

### New Capabilities

- `week-day-navigation`: Two-level day-based navigation for the weekly meal plan view
- `week-add-to-cart`: Add day's meal ingredients to shopping list from week view

### Modified Capabilities

- `meal-management`: DayMeals schema changed — UNIQUE constraint removed, multiple meals per day allowed

## Impact

- **Database**: DayMeals table schema change (UNIQUE constraint removal). Migration required for existing databases — recreate table without constraint, preserving data.
- **Handlers**: WeekCommandHandler simplified (day list only), WeekCallbackHandler extended with `day`, `to_cart` actions and new dependencies (MealIngredientRepository, ShoppingItemRepository).
- **Localization**: 5 new `week.*` keys across en/ru/pl.
- **Tests**: All week-related unit and integration tests rewritten for new UI flow. 30 tests total.
- **No breaking changes**: Existing /week data preserved through migration. Callback format changed (`week:clear:{day}:{mealId}` now includes mealId) but no external consumers.
