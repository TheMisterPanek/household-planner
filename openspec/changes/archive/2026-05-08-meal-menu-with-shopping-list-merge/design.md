## Context

The bot currently has no concept of meals or recipes. Its entire surface is the shopping list (`ShoppingItems`) plus a `Groups` table. All interaction is via command handlers and callback handlers registered in `UpdateDispatcher`. Multi-step dialogs are handled via `PendingDialogService<T>`. The existing pattern is the reference implementation for new features.

Three new SQLite tables are needed. Inline keyboard navigation across meal management (view/edit/delete/add ingredients/steps) involves many callback prefixes; this design consolidates them into a single `MealCallbackHandler` to avoid registering a dozen handlers.

## Goals / Non-Goals

**Goals:**
- `/meals` command to list, create, view, and delete meals via inline navigation
- Per-meal: ordered ingredient list (name + optional quantity), ordered cooking steps (text)
- Add/remove individual ingredients and steps from a meal view
- [🛒 Add to shopping list] button that detects name-collision conflicts and resolves them inline
- Conflict resolution: non-conflicting items added immediately; conflicting items shown in one message with per-item [Skip] / [Add anyway] buttons; message edited in place as user resolves each

**Non-Goals:**
- Meal scheduling / meal plan calendar
- Shared meal library across groups
- Ingredient quantity arithmetic (no "you already have 3l, need 2l → need 0l" auto-merge)
- Reordering steps or ingredients (future)
- Editing step text or ingredient name after creation (future)

## Decisions

### Decision 1: Single `MealCallbackHandler` routing all `meal:*` callbacks internally

**Chosen**: One `ICallbackHandler` with `CallbackPrefix = "meal:"` that parses the second segment (e.g., `view`, `delete`, `merge`, `add_ingredient`) and dispatches to private methods or injected sub-services.

**Alternatives considered**:
- One handler per callback action (10+ handlers): each is simple but results in a very long DI registration list and many nearly-identical files.
- Separate handlers per logical domain (navigation, editing, merge): cleaner split but adds indirection without meaningful isolation since they all share the same repositories.

**Why chosen**: The `UpdateDispatcher` routes by prefix, so consolidation into one handler is the natural fit for a feature with many callbacks in the same namespace. Internal routing by string-segment is AOT-safe (no reflection).

### Decision 2: Three normalized tables — `Meals`, `MealIngredients`, `MealSteps`

**Chosen**:
```sql
Meals         (Id, GroupId, Name)
MealIngredients (Id, MealId, Name, Quantity TEXT nullable)
MealSteps     (Id, MealId, StepNumber INTEGER, Text)
```

**Alternatives considered**:
- Store ingredients and steps as JSON blobs on the `Meals` row: simpler schema but breaks the ability to add/remove individual items with SQL (requires deserializing and re-serializing), harder to query, and introduces JSON reflection or source-gen complexity.
- Single `MealItems` table for both ingredients and steps with a `Type` discriminator: conflates two different ordered lists with different semantics.

**Why chosen**: Normalized design; each table has a clear single responsibility. `StepNumber` drives display order and is re-assigned (gaps permitted) when steps are removed.

### Decision 3: In-memory `MergeSession` with a short random key in callback data

**Chosen**: `MealMergeService` (singleton) holds `Dictionary<string, MergeSession>` in memory. A session is created when [🛒 Add to shopping list] is tapped. The key is an 8-character alphanumeric random string embedded in callback data (`meal:merge:skip:{key}:{idx}`). Sessions older than 15 minutes are purged on next access (lazy cleanup, no background timer).

**Alternatives considered**:
- Persist sessions to a DB table: survives restart but adds a table for data that is only valid for one UI interaction.
- Encode the full conflict list in callback data: Telegram's 64-byte callback data limit makes this impossible for even one conflict record.
- Sequential conflict resolution (one message per conflict): avoids session state but produces multiple messages; worse UX in a group chat.

**Why chosen**: Sessions are short-lived (user resolves in seconds); in-memory is sufficient. The lazy purge keeps it simple and AOT-compatible (no `Timer` reflection issues). The key is short enough to stay within the 64-byte limit.

**AOT note**: `Dictionary<string, MergeSession>` with `ConcurrentDictionary` — no reflection. ✓

### Decision 4: Conflict detection is case-insensitive name match only

**Chosen**: A conflict is any `ShoppingItem` whose `lower(Name) = lower(ingredient.Name)`. The bot shows the existing quantity and the needed quantity as informational text; the user decides whether the existing item covers it.

**Alternatives considered**:
- Substring match (e.g., "Whole Milk" matches "Milk"): too many false positives.
- Quantity-aware merge (auto-subtract): requires parsing heterogeneous quantity strings ("2л", "2 liters", "2 packs") — not feasible without a quantity DSL.

**Why chosen**: Exact-name match is unambiguous and covers the intended case (same item name, different quantity string). The human decides the semantics.

### Decision 5: Multi-step dialogs for meal creation and ingredient/step addition use `PendingDialogService<T>`

**Chosen**: Three new dialog state types:
- `MealCreateDialogState` — step 1: waiting for meal name
- `MealAddIngredientDialogState` — step 1: ingredient name, step 2: quantity; holds `MealId`
- `MealAddStepDialogState` — single step: waiting for step text; holds `MealId`

All routed through a new `MealDialogStepHandler : IDialogMessageHandler` that checks which state type is active.

**Why chosen**: Consistent with the existing `/buy` dialog pattern. No new infrastructure required.

## Sequence Diagrams

### Merge flow

```
User taps [🛒 Add to shopping list] on meal view
  → MealCallbackHandler (meal:to_list:{mealId})
      loads MealIngredients
      loads ShoppingItems for group
      separates non-conflicting vs conflicting ingredients
      inserts non-conflicting into ShoppingItems
      if no conflicts → AnswerCallbackQuery "✅ Added N items"
      if conflicts:
          creates MergeSession(conflicts=[...]) → key="a1b2c3d4"
          AnswerCallbackQuery
          SendMessage: conflict summary + inline keyboard
            row per conflict: [✓ Skip] [+ Add anyway]
            callback: "meal:merge:skip:a1b2c3d4:0", "meal:merge:add:a1b2c3d4:0"

User taps [+ Add anyway] for conflict at index 0
  → MealCallbackHandler (meal:merge:add:a1b2c3d4:0)
      loads session "a1b2c3d4"
      inserts conflict[0] ingredient into ShoppingItems
      marks conflict[0] resolved=add
      AnswerCallbackQuery
      edits conflict message: removes row 0 from keyboard
      if all resolved → edits message to final summary

User taps [✓ Skip] for conflict at index 1
  → MealCallbackHandler (meal:merge:skip:a1b2c3d4:1)
      marks conflict[1] resolved=skip
      AnswerCallbackQuery
      edits conflict message: removes row 1 from keyboard
      all resolved → edits message to final summary
```

### Meal view navigation

```
/meals
  → MealsCommandHandler
      loads Meals for group
      SendMessage: list with [View] per meal + [+ New Meal]

[View Pasta]  → meal:view:3
  → MealCallbackHandler
      loads meal 3 + ingredients + steps
      EditMessage: meal detail text + management keyboard

[➕ Ingredient] → meal:add_ingredient:3
  → MealCallbackHandler
      sets MealAddIngredientDialogState(step=1, mealId=3)
      AnswerCallbackQuery
      SendMessage: "Ingredient name?"

User types "Pasta"
  → MealDialogStepHandler (MealAddIngredientDialogState step 1)
      state.Name="Pasta", step→2
      SendMessage: "Quantity? (or /skip)"  + [Skip] button

User types "200g"
  → MealDialogStepHandler step 2
      inserts MealIngredient(mealId=3, "Pasta", "200g")
      clears dialog state
      SendMessage: "Added Pasta 200g to meal"
```

## Risks / Trade-offs

- [Merge session lost on bot restart] → User taps a merge conflict button after restart → session not found → bot answers "This request has expired, please tap 🛒 again" and ignores. Acceptable for a short-lived UX action.
- [Callback data 64-byte limit] → Longest prefix in use: `meal:remove_ingredient:` (23) + int (up to 10) + `:` + int (up to 10) = 44 bytes max. ✓ Within limit.
- [Many inline handlers in one class] → `MealCallbackHandler` will be large. Mitigated by extracting private render/helper methods and keeping each action < 20 lines.
- [StepNumber gaps after deletion] → Displayed as `1. ... 2. ... 4.` if step 3 was deleted. Acceptable for MVP; re-numbering on delete is a future enhancement.

## Migration Plan

1. `DatabaseInitializer.StartAsync` adds `CREATE TABLE IF NOT EXISTS` for `Meals`, `MealIngredients`, `MealSteps`.
2. Register three repositories and `MealMergeService` in DI.
3. Register `MealCallbackHandler` and `MealDialogStepHandler`, `MealsCommandHandler`.
4. Add `/meals` to dispatcher command routing.

**Rollback**: Remove handler registrations, drop three tables. No impact on existing data.

## Open Questions

- Should `/meals` work in private chats (personal meal library) or only in group chats? Currently assumed: group chats only (consistent with `/buy`/`/list`).
- Should deleting a meal require a confirmation tap or delete immediately?
