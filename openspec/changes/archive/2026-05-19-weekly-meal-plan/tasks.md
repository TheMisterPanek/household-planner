## 1. Add Localization Keys

- [x] 1.1 Add new `week.*` keys to `Strings.en.json`:
  - `week.btn-add-meal`, `week.btn-to-cart`
  - `week.cart-added`, `week.cart-no-ingredients`, `week.cart-no-meals`
- [x] 1.2 Add the same keys with Russian translations to `Strings.ru.json`
- [x] 1.3 Add the same keys with Polish translations to `Strings.pl.json`

## 2. Rework WeekViewBuilder

- [x] 2.1 Replace `BuildWeekKeyboard` with `BuildDayListKeyboard` — 7 day buttons in rows (2-3 per row), `week:day:{day}` callback
- [x] 2.2 Add `BuildDayDetailKeyboard(day, meals, hasMeals, localizer, chatId)` — meal rows with [✕] clear, [＋ Add meal] if under 10, [Back] + [Add to cart] bottom row

## 3. Rework WeekCallbackHandler

- [x] 3.1 Add `day` action (`week:day:{day}`) — render day detail view via `RenderDayDetailViewAsync`
- [x] 3.2 Add `to_cart` action (`week:to_cart:{day}`) — collect ingredients from day's meals, dedup against shopping list, add new items, answer callback with count
- [x] 3.3 Update `back` action — return to main day list (not week view)
- [x] 3.4 Update `assign` action — after assigning, return to day detail view
- [x] 3.5 Inject `MealIngredientRepository` and `ShoppingItemRepository` into constructor

## 4. Update WeekCommandHandler

- [x] 4.1 Use `BuildDayListKeyboard` instead of `BuildWeekKeyboard` for the main `/week` response

## 5. Update Unit Tests

- [x] 5.1 `WeekCommandHandlerTests` — main view sends day buttons (7 day labels)
- [x] 5.2 `WeekCallbackHandlerTests` — `day` action renders day detail with meals
- [x] 5.3 `WeekCallbackHandlerTests` — `to_cart` adds ingredients, answers with count
- [x] 5.4 `WeekCallbackHandlerTests` — `to_cart` with no meals answers with "no meals"
- [x] 5.5 `WeekCallbackHandlerTests` — `to_cart` with meals but no ingredients answers with "no ingredients"
- [x] 5.6 `WeekCallbackHandlerTests` — `back` returns to day list
- [x] 5.7 `WeekCallbackHandlerTests` — `assign` returns to day detail (not main view)

## 6. Update Integration Tests

- [x] 6.1 `/week` in group → sends message with day buttons
- [x] 6.2 `week:day:1` callback → edits to day detail view
- [x] 6.3 `week:assign:1:{mealId}` → persists meal, shows day detail
- [x] 6.4 `week:clear:1:{mealId}` → removes meal, shows day detail
- [x] 6.5 `week:to_cart:1` with ingredients → adds to shopping list
- [x] 6.6 `week:back` → returns to day list

## 7. Update Design Spec

- [x] 7.1 Update `design.md` with new two-level UI flow

## 8. Smoke Test (manual — do not mark complete without user confirmation)

- [x] 8.1 `/week` in private chat → "group only" response
- [x] 8.2 `/week` in group → 7 day buttons, no meal details
- [x] 8.3 Tap a day → day detail view with meals + [✕] buttons
- [x] 8.4 Tap [＋ Add meal] → meal picker; select meal → returns to day detail
- [x] 8.5 Tap [✕] on a meal → meal removed, day detail refreshed
- [x] 8.6 Tap [← Back] → returns to main day list
- [x] 8.7 Tap [🛒 Add ingredients to cart] → ingredients added to shopping list
- [x] 8.8 Tap [🛒] when no meals assigned → "no meals" toast
- [x] 8.9 Tap [🛒] when meals have no ingredients → "no ingredients" toast
