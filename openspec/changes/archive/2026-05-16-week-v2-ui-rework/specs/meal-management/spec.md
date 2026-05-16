## MODIFIED Requirements

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
