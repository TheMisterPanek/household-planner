## Context

The `/week` command manages a group's weekly meal plan. Originally it showed all 7 days with inline meal details in a flat keyboard. The v2 rework introduces day-based navigation, multi-meal support, and a shopping list integration.

**Current state**: DayMeals table has UNIQUE(GroupId, DayOfWeek), limiting to 1 meal per day. The UI shows all days inline.

**Constraints**: SQLite (no DROP CONSTRAINT), Telegram 64-byte callback limit, localization required for all user-facing text.

## Goals / Non-Goals

**Goals:**
- Two-level UI: day list → day detail
- Multiple meals per day (up to 10)
- Add day's ingredients to shopping list
- Bullet list meal display in message text
- Migration-safe for existing databases

**Non-Goals:**
- Meal reordering within a day
- Meal duplication detection
- Weekly summary/report
- Shopping list conflict resolution UI (skips duplicates silently)

## Decisions

### Two-level navigation pattern

**Decision**: Main view shows day buttons only (`week:day:{day}`). Day detail view shows meals, actions, and back button.

**Rationale**: Reduces cognitive load. Users see 7 clean buttons instead of a wall of meal details. Detail view focuses on one day at a time.

**Alternatives considered**:
- Accordion-style inline expansion: Rejected — too complex for Telegram inline keyboards
- Pagination (3-4 days per page): Rejected — adds navigation complexity without clear benefit

### Multi-meal per day via schema change

**Decision**: Remove UNIQUE(GroupId, DayOfWeek) from DayMeals table. Add migration that recreates the table without the constraint.

**Rationale**: SQLite doesn't support `ALTER TABLE DROP CONSTRAINT`. The migration creates a new table, copies data, drops old, renames. Since DayMeals data is not critical (meal assignments), this is safe.

**Alternatives considered**:
- Adding a `Slot` column (breakfast/lunch/dinner): Rejected — too rigid, users want arbitrary count
- Using a JSON column for multiple meals: Rejected — breaks JOIN queries, harder to query

### Add-to-cart scope: current day only

**Decision**: The "Add ingredients to cart" button adds ingredients from the currently viewed day's meals only.

**Rationale**: More predictable UX. User sees exactly what will be added. Per-week scope would be confusing when viewing a specific day.

**Alternatives considered**:
- Per-week "Add all": Could be added later as a separate action

### Dedup strategy for add-to-cart

**Decision**: Case-insensitive name match against existing shopping list items. Duplicates are silently skipped.

**Rationale**: Matches existing `meal:to_list` behavior. Simple and predictable. No merge conflict UI needed for this flow.

## Risks / Trade-offs

- **Migration risk** → Mitigation: Migration is idempotent (CREATE TABLE IF NOT EXISTS + try/catch). Data preserved through INSERT INTO ... SELECT.
- **10-meal limit is soft** → Mitigation: Enforced in code (GetCountAsync check) and UI (hide add button at limit). No DB constraint.
- **Callback data size** → `week:clear:{day}:{mealId}` with day (1 digit) + mealId (up to 6 digits) = ~20 chars. Well within 64-byte limit.

## Migration Plan

1. `CREATE TABLE IF NOT EXISTS DayMeals_New` without UNIQUE constraint
2. `INSERT INTO DayMeals_New SELECT FROM DayMeals`
3. `DROP TABLE DayMeals`
4. `ALTER TABLE DayMeals_New RENAME TO DayMeals`

Runs at startup in DatabaseInitializer. Wrapped in try/catch — if DayMeals doesn't exist yet, the CREATE TABLE IF NOT EXISTS handles it.
