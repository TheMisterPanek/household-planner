## Why

With the scaffold (`blazor-web-auth-scaffold`) and core inventory pages (`blazor-web-inventory-pages`) shipped, this change completes the web UI by adding the four remaining feature areas: Meals (library + ingredients + steps), Weekly Meal Plan (day assignment grid), Settings (language + expiry threshold), and AI Chat (natural-language queries). These are more complex than the inventory pages and benefit from the validated layout and auth foundation.

## What Changes

### `/meals` — Meals Library

Two-column layout: meal list sidebar + detail panel.
- List: meal name, ✎ edit / ✕ delete, [+ New meal] button.
- Detail: meal name (editable inline), ingredients table (name/qty/unit — add/delete rows), steps list (text — add/delete/reorder with ↑↓ buttons).
- Backed by `MealRepository`, `MealIngredientRepository`, `MealStepRepository`.

### `/week` — Weekly Meal Plan

7-row table (Mon–Sun). Each row: day name, assigned meal name (or "—"), dropdown to assign/clear.
- Dropdown populated from `MealRepository.GetAllAsync` + "— Clear —" option.
- On change: `DayMealsRepository.UpsertAsync` or `ClearAsync`.

### `/settings` — User Settings

Two-field form: Language (EN/RU/PL select) and Expiry notice threshold (number input, days ahead). Save button. Backed by `PreferenceRepository`.

### `/ai` — AI Chat

Chat bubble UI. User types a natural-language query, submits with Enter or button, response appears below. Backed by `AiQueryService.QueryAsync`. Message history held in component state (not persisted).

## Capabilities

### New Capabilities

- `web-meals`: Add, edit, delete meals with ingredients and steps from a browser.
- `web-week-plan`: Assign meals to days of the week from a grid view.
- `web-settings`: Change language preference and expiry threshold without bot commands.
- `web-ai`: Send natural-language inventory queries from a web chat box.

## Out of Scope

- Drag-and-drop meal reordering in the week grid (v1 uses a dropdown).
- AI response streaming (full response appears at once).
- Persisting AI chat history.

## Impact

- **Code**: 4 new Razor page components inside `ProductTrackerBot.Web`; no changes to `ProductTrackerBot` repositories or services.
- **APIs**: None new.
- **Dependencies**: No new NuGet packages.
- **Systems**: Reads/writes `Meals`, `MealIngredients`, `MealSteps`, `DayMeals`, `Preferences` tables. Calls `AiQueryService` (existing, requires `OPENROUTER_API_KEY` env var).
- **Compatibility**: AOT not applicable to web project; shared code stays AOT-safe.

## Rollback Plan

Delete `Meals.razor`, `WeekPlan.razor`, `Settings.razor`, `AiChat.razor` from `ProductTrackerBot.Web/Components/Pages/`. Remove nav links from `NavMenu.razor`. No bot-side or DB changes to revert.

## Affected Teams

Single-user/household deployment. No shared infrastructure impact.

## Cross-Cutting Notes

- Pages inherit `AuthenticatedPageBase`; unauthenticated requests redirect to `/login`.
- `DayMealsRepository` must be registered in the web project's `Program.cs` (added in this change if not already present from `blazor-web-auth-scaffold`).
- `AiQueryService` requires `OPENROUTER_API_KEY` env var; if missing, the AI page shows an error banner rather than crashing.
- No new `ILocalizer` keys needed; UI strings are rendered directly in Razor markup.
- No new `IHistoryRepository.RecordAsync` calls — meal library mutations are already recorded by bot-side service layer when triggered via Telegram; web mutations may optionally record but it is not required for v1.
