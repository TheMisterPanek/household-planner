## Why

The bot tracks what to buy but has no concept of what to cook. Groups plan meals separately and then manually add every ingredient to the shopping list — duplicating effort and causing missed or double-added items. A Meal Menu with a smart "Add to shopping list" merge resolves this by letting groups manage recipes in the bot and push their ingredients to the list in one tap, with conflict resolution when items are already there.

## What Changes

- New `/meals` command to browse, create, edit, and delete meals via inline keyboard navigation.
- Each meal has a name, an ordered ingredient list (name + optional quantity), and ordered cooking steps (text notes).
- Full CRUD on a meal: rename, add/remove ingredients, add/remove/reorder steps, delete.
- [🛒 Add to shopping list] button on a meal view: non-conflicting ingredients are added immediately; ingredients whose name already appears on the shopping list trigger an inline conflict-resolution flow where the user decides per-item whether to skip (existing covers it) or add anyway.
- Conflict message shows existing quantity vs. needed quantity and lets the user decide each item independently via inline buttons; the message is edited in place as items are resolved.

## Capabilities

### New Capabilities

- `meal-management`: Full CRUD for meals per group. Each meal holds an ordered ingredient list and ordered cooking steps. Managed entirely via inline keyboards (no separate commands for each sub-action).
- `meal-shopping-merge`: "Add to shopping list" action on a meal. Detects name-collision conflicts between meal ingredients and the active shopping list, presents them one-by-one in an inline conflict message, and adds resolved items to `ShoppingItems`.

### Modified Capabilities

_(none — existing shopping-list, telegram-bot-core requirements are unchanged at the spec level)_

## Impact

- **Database**: Three new tables — `Meals`, `MealIngredients`, `MealSteps`. No changes to existing tables.
- **Handlers**: New `MealsCommandHandler`, inline navigation callbacks (`meal:view:`, `meal:new`, `meal:edit:`, `meal:delete:`, `meal:ingredient:`, `meal:step:`), merge callbacks (`meal:merge:skip:`, `meal:merge:add:`), and step handlers for meal creation/editing dialogs.
- **Repositories**: New `MealRepository`, `MealIngredientRepository`, `MealStepRepository`.
- **Services**: New `MealMergeService` encapsulating conflict detection and resolution state.
- **New command**: `/meals` registered in the dispatcher.
- **Rollback**: Drop the three new tables, remove `/meals` command and all `meal:*` callback handlers from DI. Existing shopping list data unaffected.
- **Affected teams**: Bot feature development only.
- **AOT compatibility**: All data access via raw SQLite parameterized queries; no reflection. Inline keyboard building uses compile-time string interpolation only.
