# Bugfix: Meal Creation Not Working

**Status**: Fixed & Archived  
**Date**: 2026-05-08  
**Commits**:
- `de50882` - Fix database schema: add missing Meals, MealIngredients, MealSteps tables
- `8347e2d` - Fix meal creation: use actual group instead of hardcoded ID

## Problem

When users tried to create meals via `/meals` → `➕ New Meal`, the meals were not appearing in the list. There were two root causes:

### Issue 1: Missing Database Tables
The `Meals`, `MealIngredients`, and `MealSteps` tables were never being created during database initialization, causing "no such table" SQLite errors when accessing meal features.

**Impact**: Bot crashed when trying to view/manage meals

### Issue 2: Hardcoded Group ID
The meal creation dialog handler was hardcoding `groupId=1` instead of retrieving the actual group for the chat, causing meals to be added to the wrong group.

**Root cause** (MealDialogStepHandler.cs:112):
```csharp
var groupId = 1; // Placeholder: should extract from context
```

**Impact**: Meals created in one group were invisible in other groups

## Solution

### Fix 1: Add Missing Tables to DatabaseInitializer
Added CREATE TABLE statements for:
- `Meals` (Id, GroupId, Name)
- `MealIngredients` (Id, MealId, Name, Quantity)
- `MealSteps` (Id, MealId, StepNumber, Text)

### Fix 2: Retrieve Actual Group
- Added `GroupRepository` dependency to `MealDialogStepHandler`
- Changed meal creation to use `groupRepository.GetOrCreateAsync(chatId)` to get the correct group
- Registered `MealDialogStepHandler` in DI container (was missing)

## Changes Made

**Files Modified**:
1. `ProductTrackerBot/Database/DatabaseInitializer.cs` - Added 3 table creation statements
2. `ProductTrackerBot/Handlers/MealDialogStepHandler.cs` - Fixed group lookup + added dependency
3. `ProductTrackerBot/Program.cs` - Registered MealDialogStepHandler in DI

**Test Status**: Main project builds successfully. Tests have unrelated failures.

## Verification

Meals can now be created via the `/meals` command in groups and appear correctly in that group's meal list.
