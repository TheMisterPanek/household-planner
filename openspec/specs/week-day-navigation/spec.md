# week-day-navigation Specification

## Purpose
Provides a day-based navigation UI for the weekly meal plan, replacing the previous single-view layout with a two-level flow: a day list view and a day detail view with per-meal management controls.

## Requirements

### Requirement: Day list view
When the user sends `/week` in a group chat, the system SHALL display a message with 7 day buttons (Mon–Sun) arranged in rows of 3. No meal details SHALL be shown in this view.

#### Scenario: Group chat shows day buttons
- **WHEN** user sends `/week` in a group chat
- **THEN** bot sends a message with header "📅 Meal plan:" and 7 inline buttons (Mon, Tue, Wed, Thu, Fri, Sat, Sun) with callback data `week:day:{1-7}`

#### Scenario: Private chat rejected
- **WHEN** user sends `/week` in a private chat
- **THEN** bot replies with the localized "common.group-only" message

---

### Requirement: Day detail view
When the user taps a day button, the system SHALL display the day's meals as a bullet list in the message text, with per-meal clear buttons, an add-meal button, a back button, and an add-to-cart button.

#### Scenario: Day with meals shows bullet list
- **WHEN** user taps Monday and Monday has "Pasta" and "Soup" assigned
- **THEN** message text shows "📅 Mon:" followed by "• Pasta" and "• Soup" on separate lines
- **AND** keyboard shows [Pasta][✕], [Soup][✕], [＋ Add meal], [← Back][🛒 Add ingredients to cart]

#### Scenario: Day with no meals
- **WHEN** user taps Wednesday and no meals are assigned
- **THEN** message text shows "📅 Wed:" with no bullet list
- **AND** keyboard shows [＋ Add meal] and [← Back][🛒 Add ingredients to cart]

---

### Requirement: Meal picker
When the user taps "Add meal" in the day detail view, the system SHALL display a meal picker with all meals from the group's library.

#### Scenario: Meal picker shows available meals
- **WHEN** user taps [＋ Add meal] for Monday and the group has meals "Pasta", "Pizza", "Soup"
- **THEN** message shows "Choose a meal for Mon:" with buttons [Pasta], [Pizza], [Soup], [← Back]
- **AND** each meal button has callback data `week:assign:{day}:{mealId}`

#### Scenario: No meals in library
- **WHEN** user taps [＋ Add meal] and the group has no meals in the library
- **THEN** bot answers callback with localized "week.no-meals" toast

---

### Requirement: Assign meal to day
When the user taps a meal in the picker, the system SHALL assign it to the day and return to the day detail view.

#### Scenario: Successful assignment
- **WHEN** user taps "Pasta" in the picker for Monday
- **THEN** meal is persisted in DayMeals table
- **AND** message updates to show day detail view with "Pasta" in the bullet list

#### Scenario: Maximum meals reached
- **WHEN** user tries to assign an 11th meal to a day
- **THEN** bot answers callback with localized "week.max-meals" toast
- **AND** meal is NOT assigned

---

### Requirement: Clear meal from day
When the user taps the clear button (✕) next to a meal, the system SHALL remove that specific meal from the day.

#### Scenario: Clear specific meal
- **WHEN** user taps [✕] next to "Pasta" on Monday
- **THEN** "Pasta" is removed from Monday in DayMeals table
- **AND** day detail view refreshes without "Pasta"

---

### Requirement: Back to day list
When the user taps the back button, the system SHALL return to the main day list view.

#### Scenario: Back returns to day list
- **WHEN** user taps [← Back] in day detail view
- **THEN** message updates to show the 7-day button layout (main view)

---

### Requirement: Multi-meal per day
The system SHALL allow assigning up to 10 meals to a single day. The DayMeals table SHALL NOT have a UNIQUE constraint on (GroupId, DayOfWeek).

#### Schema: DayMeals table without UNIQUE constraint
- **GIVEN** the DayMeals table has columns Id, GroupId, DayOfWeek, MealId
- **WHEN** multiple rows exist with the same GroupId and DayOfWeek
- **THEN** all rows are stored and returned

---

### Requirement: Database migration
The system SHALL migrate existing DayMeals tables that have the UNIQUE constraint by recreating the table without it.

#### Scenario: Migration on existing database
- **GIVEN** an existing DayMeals table with UNIQUE(GroupId, DayOfWeek) and data
- **WHEN** the bot starts up
- **THEN** data is preserved in the new table without UNIQUE constraint
