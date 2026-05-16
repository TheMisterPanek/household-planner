## 1. Localization Keys

- [ ] 1.1 Add `week.btn-add-meal`, `week.btn-to-cart`, `week.cart-added`, `week.cart-no-ingredients`, `week.cart-no-meals` to `Strings.en.json`
- [ ] 1.2 Add the same keys with Russian translations to `Strings.ru.json`
- [ ] 1.3 Add the same keys with Polish translations to `Strings.pl.json`

## 2. Database Schema

- [ ] 2.1 Remove UNIQUE(GroupId, DayOfWeek) from DayMeals CREATE TABLE DDL in `DatabaseInitializer.cs`
- [ ] 2.2 Add migration block: recreate DayMeals table without UNIQUE constraint, preserving existing data

## 3. DayMealsRepository

- [ ] 3.1 Replace `UpsertAsync` with `InsertAsync` (plain INSERT, no INSERT OR REPLACE)
- [ ] 3.2 Add `GetCountAsync(int groupId, int dayOfWeek)` — returns count of meals for a day
- [ ] 3.3 Add `ClearMealAsync(int groupId, int dayOfWeek, int mealId)` — removes specific meal from day

## 4. WeekViewBuilder

- [ ] 4.1 Create `BuildDayListKeyboard(localizer, chatId)` — 7 day buttons in rows of 3, `week:day:{day}` callback
- [ ] 4.2 Create `BuildDayDetailKeyboard(day, dayMeals, hasMeals, localizer, chatId)` — meal rows with [✕], [＋ Add meal], [Back] + [Add to cart]
- [ ] 4.3 Remove old `BuildWeekKeyboard` method

## 5. WeekCommandHandler

- [ ] 5.1 Simplify to show day list only — remove DayMealsRepository and MealRepository dependencies
- [ ] 5.2 Use `BuildDayListKeyboard` instead of `BuildWeekKeyboard`

## 6. WeekCallbackHandler

- [ ] 6.1 Add `day` action (`week:day:{day}`) — render day detail view with bullet list
- [ ] 6.2 Add `to_cart` action (`week:to_cart:{day}`) — collect day's ingredients, dedup against shopping list, add new items
- [ ] 6.3 Update `back` action — return to day list (not week view)
- [ ] 6.4 Update `assign` action — return to day detail view after assignment
- [ ] 6.5 Inject `MealIngredientRepository` and `ShoppingItemRepository` into constructor
- [ ] 6.6 Update `RenderDayDetailViewAsync` to show bullet list of meals in message text

## 7. Unit Tests

- [ ] 7.1 `WeekCommandHandlerTests` — private chat group-only guard, main view sends day buttons
- [ ] 7.2 `WeekCallbackHandlerTests` — `day` action renders day detail with bullet list
- [ ] 7.3 `WeekCallbackHandlerTests` — `pick` shows meal picker, `pick` with no meals answers toast
- [ ] 7.4 `WeekCallbackHandlerTests` — `assign` inserts and shows day detail, max-meals check
- [ ] 7.5 `WeekCallbackHandlerTests` — `clear` removes specific meal and shows day detail
- [ ] 7.6 `WeekCallbackHandlerTests` — `back` returns to day list
- [ ] 7.7 `WeekCallbackHandlerTests` — `to_cart` adds ingredients, answers count, handles no-meals and no-ingredients
- [ ] 7.8 `WeekCallbackHandlerTests` — `noop` answers callback only
- [ ] 7.9 `DayMealsRepositoryTests` — InsertAsync, multiple meals per day, GetCountAsync, ClearAsync, ClearMealAsync

## 8. Integration Tests

- [ ] 8.1 `/week` in private chat → group-only message
- [ ] 8.2 `/week` in group → sends header with day buttons
- [ ] 8.3 `week:day:1` → edits to day detail with day header
- [ ] 8.4 `week:assign:1:{mealId}` → persists meal, shows day detail with bullet list
- [ ] 8.5 `week:clear:1:{mealId}` → removes specific meal
- [ ] 8.6 `week:pick:1` with meals → edits to meal picker
- [ ] 8.7 `week:pick:1` without meals → answers no-meals toast
- [ ] 8.8 `week:back` → returns to day list
- [ ] 8.9 `week:to_cart:1` with ingredients → adds to shopping list
- [ ] 8.10 `week:to_cart:1` with no meals → answers no-meals toast

## 9. OpenRouter Timeout

- [ ] 9.1 Increase OpenRouter HTTP client timeout from 30s to 2min in `Program.cs`

## 10. Smoke Test (manual — do not mark complete without user confirmation)

- [ ] 10.1 `/week` in private chat → "group only" response
- [ ] 10.2 `/week` in group → 7 day buttons, no meal details
- [ ] 10.3 Tap a day → day detail with bullet list of meals
- [ ] 10.4 Tap [＋ Add meal] → meal picker; select meal → returns to day detail
- [ ] 10.5 Tap [✕] on a meal → meal removed, day detail refreshed
- [ ] 10.6 Tap [← Back] → returns to main day list
- [ ] 10.7 Tap [🛒 Add ingredients to cart] → ingredients added to shopping list
- [ ] 10.8 Tap [🛒] when no meals → "no meals" toast
- [ ] 10.9 Tap [🛒] when meals have no ingredients → "no ingredients" toast
- [ ] 10.10 Verify /week command appears in Telegram command menu
