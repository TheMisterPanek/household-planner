# meal-management Specification

## Purpose
TBD - created by archiving change meal-menu-with-shopping-list-merge. Update Purpose after archive.
## Requirements
### Requirement: /meals command shows the group's meal list
The system SHALL register a `/meals` command that replies with a message listing all meals for the group. Each meal is shown with a [View] button (callback `meal:view:{mealId}`). A [➕ New Meal] button (callback `meal:new`) is always shown. The command SHALL only function in group chats.

#### Scenario: Group has meals
- **WHEN** a group member sends `/meals` and the group has at least one meal
- **THEN** the bot sends a message listing each meal name with a [View] button and a [➕ New Meal] button

#### Scenario: Group has no meals
- **WHEN** a group member sends `/meals` and the group has no meals
- **THEN** the bot sends "No meals yet." with only a [➕ New Meal] button

#### Scenario: /meals in private chat
- **WHEN** a user sends `/meals` in a private chat
- **THEN** the bot replies "This command only works in group chats." and takes no further action

---

### Requirement: New meal is created via inline dialog
Tapping [➕ New Meal] SHALL set a `MealCreateDialogState` for the tapping user and prompt for a meal name. The name entered becomes the meal's name. The meal is inserted into the `Meals` table linked to the group.

#### Scenario: User creates a meal
- **WHEN** a user taps [➕ New Meal] then replies with "Pasta Carbonara"
- **THEN** a `Meals` row is inserted with `Name = "Pasta Carbonara"` and `GroupId` of the chat, and the bot replies "🍝 Pasta Carbonara created!" and sends the meal view

#### Scenario: Empty name is rejected
- **WHEN** a user replies with a blank or whitespace-only string as the meal name
- **THEN** the bot replies "Name can't be empty — try again" and remains in the dialog

---

### Requirement: Meal view shows ingredients, steps, and management buttons
Tapping [View] on a meal (callback `meal:view:{mealId}`) SHALL edit or send a message displaying the meal name, ingredients list, and numbered steps. The inline keyboard SHALL include: [➕ Ingredient], [➕ Step], [🛒 Add to list], [🗑 Delete meal], [← Meals].

#### Scenario: Meal with ingredients and steps
- **WHEN** a user taps [View] on a meal that has ingredients and steps
- **THEN** the bot displays the meal name, a bullet list of ingredients (name + quantity), and a numbered list of steps, with the management keyboard

#### Scenario: Meal with no ingredients or steps
- **WHEN** a user taps [View] on an empty meal
- **THEN** the bot displays the meal name with "(no ingredients)" and "(no steps)" placeholders and the management keyboard

#### Scenario: Each ingredient shown with a remove button
- **WHEN** the meal view message is displayed
- **THEN** each ingredient has a `[✗ Name qty]` button in the keyboard (callback `meal:remove_ingredient:{mealId}:{ingredientId}`) so individual ingredients can be deleted

#### Scenario: Each step shown with a remove button
- **WHEN** the meal view message is displayed
- **THEN** each step has a `[✗ Step N]` button in the keyboard (callback `meal:remove_step:{mealId}:{stepId}`) so individual steps can be deleted

---

### Requirement: Ingredient is added to a meal via dialog
Tapping [➕ Ingredient] (callback `meal:add_ingredient:{mealId}`) SHALL set a `MealAddIngredientDialogState(step=1, mealId)` and prompt for the ingredient name. A second step asks for quantity with a [Skip] option.

#### Scenario: Ingredient added with quantity
- **WHEN** a user taps [➕ Ingredient], replies "Pasta", then replies "200g"
- **THEN** a `MealIngredients` row is inserted with `Name = "Pasta"`, `Quantity = "200g"`, and the bot confirms "Added Pasta 200g" and refreshes the meal view

#### Scenario: Ingredient added without quantity
- **WHEN** a user taps [➕ Ingredient], replies "Salt", then taps [Skip]
- **THEN** a `MealIngredients` row is inserted with `Quantity = null` and the bot confirms "Added Salt" and refreshes the meal view

---

### Requirement: Ingredient is removed from a meal
Tapping `[✗ Name qty]` (callback `meal:remove_ingredient:{mealId}:{ingredientId}`) SHALL delete the `MealIngredients` row and refresh the meal view message.

#### Scenario: Ingredient removed
- **WHEN** a user taps the remove button for "Pasta 200g"
- **THEN** the `MealIngredients` row is deleted and the meal view is refreshed showing the ingredient no longer present

---

### Requirement: Cooking step is added to a meal via dialog
Tapping [➕ Step] (callback `meal:add_step:{mealId}`) SHALL set a `MealAddStepDialogState(mealId)` and prompt for step text. The step is inserted with `StepNumber = max(existing) + 1`.

#### Scenario: Step added
- **WHEN** a user taps [➕ Step] and replies "Boil pasta in salted water for 10 minutes"
- **THEN** a `MealSteps` row is inserted and the bot confirms "Added step N" and refreshes the meal view

---

### Requirement: Cooking step is removed from a meal
Tapping `[✗ Step N]` (callback `meal:remove_step:{mealId}:{stepId}`) SHALL delete the `MealSteps` row. Remaining steps retain their existing `StepNumber` values (no renumbering).

#### Scenario: Step removed
- **WHEN** a user taps the remove button for step 2
- **THEN** the `MealSteps` row is deleted and the meal view is refreshed showing the step no longer present

---

### Requirement: Meal is deleted with confirmation
Tapping [🗑 Delete meal] (callback `meal:delete:{mealId}`) SHALL ask for confirmation. Tapping [Yes, delete] (callback `meal:delete_confirm:{mealId}`) deletes the meal and all its ingredients and steps. Tapping [Cancel] returns to the meal view.

#### Scenario: User confirms deletion
- **WHEN** a user taps [🗑 Delete meal] then [Yes, delete]
- **THEN** the `Meals` row and all associated `MealIngredients` and `MealSteps` rows are deleted, and the bot shows the updated meal list

#### Scenario: User cancels deletion
- **WHEN** a user taps [🗑 Delete meal] then [Cancel]
- **THEN** no rows are deleted and the meal view is shown again

---

### Requirement: Meal data is persisted in three normalized tables
The system SHALL create `Meals(Id, GroupId, Name)`, `MealIngredients(Id, MealId, Name, Quantity)`, and `MealSteps(Id, MealId, StepNumber, Text)` in SQLite via `DatabaseInitializer`. All foreign keys reference parent rows. Deleting a `Meals` row SHALL cascade-delete its `MealIngredients` and `MealSteps`.

#### Scenario: Tables created on fresh database
- **WHEN** the application starts and none of the three tables exist
- **THEN** all three tables are created before any commands are processed

#### Scenario: Table creation is idempotent
- **WHEN** the application starts and the tables already exist
- **THEN** no error occurs and existing data is preserved

---

### Requirement: DayMeals table schema
The DayMeals table SHALL store meal assignments per day per group. It SHALL NOT enforce a UNIQUE constraint on (GroupId, DayOfWeek), allowing multiple meals per day.

**Previous behavior**: UNIQUE(GroupId, DayOfWeek) limited to 1 meal per day.

**Changed behavior**: No UNIQUE constraint. Multiple meals per day allowed, up to 10 enforced in application code.

#### Schema
```sql
CREATE TABLE IF NOT EXISTS DayMeals (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    GroupId   INTEGER NOT NULL REFERENCES Groups(Id),
    DayOfWeek INTEGER NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),
    MealId    INTEGER NOT NULL REFERENCES Meals(Id)
);
```

#### Scenario: Multiple meals per day
- **GIVEN** Monday already has "Pasta" assigned
- **WHEN** user assigns "Soup" to Monday
- **THEN** both "Pasta" and "Soup" are stored for Monday

#### Scenario: Clear specific meal
- **GIVEN** Monday has "Pasta" (mealId=1) and "Soup" (mealId=2)
- **WHEN** user clears mealId=1 from Monday
- **THEN** only "Pasta" is removed; "Soup" remains

