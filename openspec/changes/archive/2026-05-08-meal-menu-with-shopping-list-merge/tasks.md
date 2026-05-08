## 1. Database Schema

- [x] 1.1 Add `CREATE TABLE IF NOT EXISTS Meals (Id INTEGER PRIMARY KEY AUTOINCREMENT, GroupId INTEGER NOT NULL REFERENCES Groups(Id), Name TEXT NOT NULL)` in `DatabaseInitializer.StartAsync`
- [x] 1.2 Add `CREATE TABLE IF NOT EXISTS MealIngredients (Id INTEGER PRIMARY KEY AUTOINCREMENT, MealId INTEGER NOT NULL REFERENCES Meals(Id) ON DELETE CASCADE, Name TEXT NOT NULL, Quantity TEXT)` in `DatabaseInitializer.StartAsync`
- [x] 1.3 Add `CREATE TABLE IF NOT EXISTS MealSteps (Id INTEGER PRIMARY KEY AUTOINCREMENT, MealId INTEGER NOT NULL REFERENCES Meals(Id) ON DELETE CASCADE, StepNumber INTEGER NOT NULL, Text TEXT NOT NULL)` in `DatabaseInitializer.StartAsync`

## 2. Models

- [x] 2.1 Create `Meal` record: `Id`, `GroupId`, `Name`
- [x] 2.2 Create `MealIngredient` record: `Id`, `MealId`, `Name`, `Quantity` (nullable)
- [x] 2.3 Create `MealStep` record: `Id`, `MealId`, `StepNumber`, `Text`
- [x] 2.4 Create `MealCreateDialogState` class: `Step` (always 1 for now — awaiting name)
- [x] 2.5 Create `MealAddIngredientDialogState` class: `Step` (1 = name, 2 = quantity), `MealId`, `Name` (nullable)
- [x] 2.6 Create `MealAddStepDialogState` class: `MealId`

## 3. Repositories

- [x] 3.1 Create `MealRepository` with: `GetAllAsync(groupId)`, `GetByIdAsync(mealId)`, `AddAsync(groupId, name)`, `DeleteAsync(mealId)`
- [x] 3.2 Create `MealIngredientRepository` with: `GetAllAsync(mealId)`, `AddAsync(mealId, name, quantity?)`, `DeleteAsync(ingredientId)`
- [x] 3.3 Create `MealStepRepository` with: `GetAllAsync(mealId)`, `AddAsync(mealId, text)` (auto-assigns StepNumber = max+1), `DeleteAsync(stepId)`
- [x] 3.4 Register all three repositories in `Program.cs` DI

## 4. Unit Tests — Repositories

- [x] 4.1 Test `MealRepository.AddAsync` inserts correctly and `GetAllAsync` returns only meals for the given group
- [x] 4.2 Test `MealRepository.DeleteAsync` cascades to `MealIngredients` and `MealSteps`
- [x] 4.3 Test `MealIngredientRepository.AddAsync` with and without quantity
- [x] 4.4 Test `MealStepRepository.AddAsync` assigns incrementing `StepNumber` relative to existing steps
- [x] 4.5 Test `MealStepRepository.DeleteAsync` removes correct step without affecting others

## 5. MealMergeService

- [x] 5.1 Create `MergeConflict` class: `IngredientName`, `MealQuantity` (nullable), `ExistingQuantity` (nullable)
- [x] 5.2 Create `MergeSession` class: `ChatId`, `MealId`, `CreatedAt`, `Conflicts: List<MergeConflict>`, `Resolved: Dictionary<int, bool>` (index → wasAdded)
- [x] 5.3 Implement `MealMergeService` (singleton) with `ConcurrentDictionary<string, MergeSession>`: `CreateSession(chatId, mealId, conflicts)` returns sessionId; `TryGetSession(sessionId)` with lazy 15-minute purge; `ResolveConflict(sessionId, index, add)`
- [x] 5.4 Register `MealMergeService` as singleton in `Program.cs`

## 6. Unit Tests — MealMergeService

- [x] 6.1 Test `CreateSession` returns an 8-char alphanumeric key and session is retrievable
- [x] 6.2 Test `TryGetSession` returns null for unknown key
- [x] 6.3 Test lazy purge removes sessions older than 15 minutes on next `TryGetSession` call
- [x] 6.4 Test `ResolveConflict` marks index as resolved and `MergeSession.Resolved` reflects the add/skip decision

## 7. Meal Dialog Step Handler

- [x] 7.1 Create `MealDialogStepHandler : IDialogMessageHandler` that checks (in order) for `MealCreateDialogState`, `MealAddIngredientDialogState`, `MealAddStepDialogState` via `PendingDialogService<T>`
- [x] 7.2 Implement `MealCreateDialogState` handling: reject blank names; on valid name insert meal, clear state, send confirmation + meal view
- [x] 7.3 Implement `MealAddIngredientDialogState` step 1: save name to state, advance to step 2, ask for quantity with [Skip] button (callback `meal:ingredient_skip:{mealId}`)
- [x] 7.4 Implement `MealAddIngredientDialogState` step 2: insert ingredient with entered quantity, clear state, send confirmation + refresh meal view
- [x] 7.5 Implement `MealAddStepDialogState`: insert step, clear state, send confirmation + refresh meal view
- [x] 7.6 Register `PendingDialogService<MealCreateDialogState>`, `PendingDialogService<MealAddIngredientDialogState>`, `PendingDialogService<MealAddStepDialogState>` as singletons in `Program.cs`
- [x] 7.7 Register `MealDialogStepHandler` in `Program.cs`

## 8. MealsCommandHandler

- [x] 8.1 Create `MealsCommandHandler : ICommandHandler` for `/meals`; reject private chats; load meals for group; send list message with [View] buttons per meal and [➕ New Meal] button
- [x] 8.2 Register `MealsCommandHandler` in `Program.cs`

## 9. MealCallbackHandler — Navigation

- [x] 9.1 Create `MealCallbackHandler : ICallbackHandler` with `CallbackPrefix = "meal:"` and internal routing on the second path segment
- [x] 9.2 Implement `meal:list` action: reload meal list and edit the message
- [x] 9.3 Implement `meal:view:{mealId}` action: load meal + ingredients + steps, build meal detail text and management keyboard (ingredient remove buttons, step remove buttons, [➕ Ingredient], [➕ Step], [🛒 Add to list], [🗑 Delete meal], [← Meals])
- [x] 9.4 Implement `meal:new` action: set `MealCreateDialogState` for the tapping user, answer callback, send "Meal name?" prompt
- [x] 9.5 Implement `meal:delete:{mealId}` action: edit message with confirmation keyboard [Yes, delete] / [Cancel]
- [x] 9.6 Implement `meal:delete_confirm:{mealId}` action: delete meal (cascade), answer callback, send updated meal list

## 10. MealCallbackHandler — Ingredient & Step Actions

- [x] 10.1 Implement `meal:add_ingredient:{mealId}` action: set `MealAddIngredientDialogState(step=1, mealId)`, answer callback, send "Ingredient name?" prompt
- [x] 10.2 Implement `meal:ingredient_skip:{mealId}` action: read state step — if step 2, insert ingredient with `Quantity = null`, clear state, refresh meal view
- [x] 10.3 Implement `meal:remove_ingredient:{mealId}:{ingredientId}` action: delete ingredient, refresh meal view
- [x] 10.4 Implement `meal:add_step:{mealId}` action: set `MealAddStepDialogState(mealId)`, answer callback, send "Step text?" prompt
- [x] 10.5 Implement `meal:remove_step:{mealId}:{stepId}` action: delete step, refresh meal view

## 11. MealCallbackHandler — Shopping List Merge

- [x] 11.1 Implement `meal:to_list:{mealId}` action: load ingredients + current shopping items; separate into non-conflicting and conflicting; insert non-conflicting; if no conflicts answer callback with "✅ Added N items"; if conflicts create merge session and send conflict message
- [x] 11.2 Build conflict message text and keyboard: one row per unresolved conflict showing `{Name} — on list: {existing qty} | needed: {meal qty}` with `[✓ Skip]` and `[+ Add anyway]` buttons
- [x] 11.3 Implement `meal:merge:skip:{sessionId}:{idx}` action: `TryGetSession`; if null, answer "expired"; else `ResolveConflict(skip)`, edit conflict message removing resolved row, if all done edit to summary
- [x] 11.4 Implement `meal:merge:add:{sessionId}:{idx}` action: `TryGetSession`; if null, answer "expired"; else insert `ShoppingItem`, `ResolveConflict(add)`, edit conflict message removing resolved row, if all done edit to summary
- [x] 11.5 Implement final summary message builder: "✅ Done! Added: {names}. Skipped: {names}." (omit empty sections)

## 12. Unit Tests — Handlers

- [x] 12.1 Test `MealsCommandHandler` replies with usage error in private chat
- [x] 12.2 Test `MealsCommandHandler` sends meal list with correct button count for a group with N meals
- [x] 12.3 Test `MealDialogStepHandler` rejects blank meal name and remains in dialog
- [x] 12.4 Test `MealDialogStepHandler` creates meal on valid name and clears dialog state
- [x] 12.5 Test `MealDialogStepHandler` ingredient step 1 → step 2 transition (name saved, quantity prompt sent)
- [x] 12.6 Test `MealDialogStepHandler` ingredient step 2 inserts row and clears state
- [x] 12.7 Test `MealCallbackHandler` `meal:to_list` inserts non-conflicting items and sends "Added N" answer when no conflicts
- [x] 12.8 Test `MealCallbackHandler` `meal:to_list` creates merge session and sends conflict message when conflicts exist
- [x] 12.9 Test `MealCallbackHandler` `meal:merge:add` inserts shopping item and edits conflict message
- [x] 12.10 Test `MealCallbackHandler` `meal:merge:skip` does not insert shopping item and edits conflict message
- [x] 12.11 Test `MealCallbackHandler` `meal:merge:*` answers "expired" when session not found
- [x] 12.12 Test final summary is sent when all conflicts are resolved
