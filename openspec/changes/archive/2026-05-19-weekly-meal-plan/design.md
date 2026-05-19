# Weekly Meal Plan — Design (v2)

## Overview

Two-level day-based navigation for the `/week` command. Main view shows 7 day buttons only. Tapping a day shows its meals with per-meal management and an "Add ingredients to cart" action.

## UI Flow

### Main View (`/week`)
```
📅 Meal plan:
[Monday] [Tuesday] [Wednesday]
[Thursday] [Friday] [Saturday] [Sunday]
```
- Each button: `week:day:{day}` (1=Mon, 7=Sun)
- 2-3 buttons per row for compact layout
- Shows day names only, no meal details

### Day View (`week:day:{day}`)
```
📅 Monday:
[Pasta]          [✕]
[Soup]           [✕]
[＋ Add meal]
[← Back] [🛒 Add ingredients to cart]
```
- Header: `📅 {dayName}:` with meal count
- Each meal: label button (`week:noop`) + clear button (`week:clear:{day}:{mealId}`)
- If under 10 meals: [＋ Add meal] button (`week:pick:{day}`)
- Bottom row: [← Back] (`week:back`) + [🛒 Add ingredients to cart] (`week:to_cart:{day}`)

### Meal Picker (`week:pick:{day}`)
```
Choose a meal for Monday:
[Pasta] [Pizza] [Soup]
[← Back]
```
- Same as current picker
- After selection: returns to day view

### Add to Cart (`week:to_cart:{day}`)
- Collects ingredients from meals assigned to the **current day only**
- Deduplicates by name (case-insensitive)
- Skips items already on the shopping list
- Adds via `ShoppingItemRepository.AddAsync`
- Answers callback: "✓ Added N items" or "No ingredients to add"

## Callback Data Format

| Callback | Action |
|---|---|
| `week:day:{day}` | Show day detail view |
| `week:pick:{day}` | Show meal picker for day |
| `week:assign:{day}:{mealId}` | Assign meal to day, return to day view |
| `week:clear:{day}:{mealId}` | Remove specific meal from day |
| `week:to_cart:{day}` | Add day's ingredients to shopping list |
| `week:back` | Return to main day list view |
| `week:noop` | No-op (label buttons) |

## Data Access

- `DayMealsRepository.GetWeekAsync(groupId)` — already exists
- `DayMealsRepository.InsertAsync(groupId, dayOfWeek, mealId)` — already exists
- `DayMealsRepository.ClearMealAsync(groupId, dayOfWeek, mealId)` — already exists
- `DayMealsRepository.GetCountAsync(groupId, dayOfWeek)` — already exists
- `MealIngredientRepository.GetAllAsync(mealId)` — already exists (for collecting ingredients)
- `ShoppingItemRepository.AddAsync(groupId, name, quantity, addedByName)` — already exists
- `ShoppingItemRepository.GetAllAsync(groupId)` — already exists (for dedup check)

## Localization Keys

Keep existing: `week.header`, `week.pick-prompt`, `week.no-meals`, `week.btn-set`, `week.btn-clear`, `week.btn-back`, `week.day.1-7`, `week.max-meals`

New keys:
- `week.btn-add-meal` — "＋ Add meal"
- `week.btn-to-cart` — "🛒 Add ingredients to cart"
- `week.cart-added` — "✓ Added {0} items to shopping list"
- `week.cart-no-ingredients` — "No ingredients found for this day's meals"
- `week.cart-no-meals` — "No meals assigned to this day"

## File Changes

| File | Change |
|---|---|
| `Handlers/WeekViewBuilder.cs` | Replace `BuildWeekKeyboard` with `BuildDayListKeyboard` and `BuildDayDetailKeyboard` |
| `Handlers/WeekCallbackHandler.cs` | Add `day`, `to_cart` actions; update `back`; inject `MealIngredientRepository`, `ShoppingItemRepository` |
| `Handlers/WeekCommandHandler.cs` | Use `BuildDayListKeyboard` |
| `Localization/Strings.*.json` | Add 5 new keys |
| `Tests/*` | Update all week tests |
