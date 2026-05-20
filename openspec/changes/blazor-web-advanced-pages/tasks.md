## Prerequisites

- `blazor-web-auth-scaffold` merged
- `blazor-web-inventory-pages` merged (or at least the layout/auth base in place)

---

## 1. DI Registration in `ProductTrackerBot.Web/Program.cs`

- [x] 1.1 Register `MealRepository` as `AddScoped`
- [x] 1.2 Register `MealIngredientRepository` as `AddScoped`
- [x] 1.3 Register `MealStepRepository` as `AddScoped`
- [x] 1.4 Register `DayMealsRepository` as `AddScoped`
- [x] 1.5 Register `MealMergeService` as `AddScoped`
- [x] 1.6 Register `IAiQueryService` → `AiQueryService` as `AddScoped`
- [x] 1.7 Register `IOpenRouterClient` → `OpenRouterClient` via `AddHttpClient<IOpenRouterClient, OpenRouterClient>()`
- [x] 1.8 Register `ConversationHistoryService` as `AddSingleton` if not already present

---

## 2. `/meals` — Meals Library Page

- [x] 2.1 Create `Components/Pages/Meals.razor` inheriting `AuthenticatedPageBase`:
  - Load meal list via `MealRepository.GetAllAsync(groupId)`
  - Two-column layout: meal list sidebar (left), detail panel (right)
- [x] 2.2 Meal list sidebar:
  - Renders meal names; clicking a meal loads detail into panel (`_selected = meal`; loads ingredients + steps)
  - [+ New meal] button at top: shows inline name input; on submit calls `MealRepository.AddAsync(groupId, name)` → reload list → auto-select new meal
  - Each meal row has ✕ button: calls `MealRepository.DeleteAsync(mealId)` with inline confirmation; reload list
- [x] 2.3 Detail panel — meal name:
  - Shows meal name with a [Rename] button
  - [Rename] → name becomes an input with Save/Cancel
  - Save → `MealRepository.UpdateNameAsync(mealId, name)` → reload
- [x] 2.4 Detail panel — ingredients table:
  - Table with columns: Name, Qty/Unit, ✕
  - [+ Add] button shows a new-row form below the table: Name input, Qty input, Unit input; Submit → `MealIngredientRepository.UpsertAsync(mealId, ...)` → reload; Cancel → collapse
  - ✕ per row → `MealIngredientRepository.DeleteAsync(ingredientId)` with inline confirmation → reload
- [x] 2.5 Detail panel — steps list:
  - Numbered list; each row: step text, ↑ ↓ (reorder), ✎ (edit text), ✕ (delete)
  - ✎ → text becomes an input; Save → `MealStepRepository.UpdateAsync(stepId, text)` → reload
  - ↑ / ↓ → swap `Order` values of adjacent steps via two `UpdateAsync` calls → reload
  - ✕ → `MealStepRepository.DeleteAsync(stepId)` with inline confirmation → reload
  - [+ Add] button at bottom → new text input; Submit → `MealStepRepository.AddAsync(mealId, text, maxOrder+1)` → reload

---

## 3. Unit Tests — `/meals`

- [x] 3.1 Clicking a meal in the sidebar loads its ingredients and steps in the detail panel
- [x] 3.2 [+ New meal] → `AddAsync` called; new meal appears in list
- [x] 3.3 ✕ on meal with confirmation → `DeleteAsync` called; meal removed from list
- [x] 3.4 [Rename] → `UpdateNameAsync` called with new name
- [x] 3.5 [+ Add] ingredient → `UpsertAsync` called with correct args
- [x] 3.6 ↑ on step 2 → swaps Order with step 1; steps rerender in new order
- [x] 3.7 ✎ on step → `UpdateAsync` called with edited text

---

## 4. `/week` — Weekly Meal Plan Page

- [x] 4.1 Create `Components/Pages/WeekPlan.razor` inheriting `AuthenticatedPageBase`:
  - Load plan via `DayMealsRepository.GetWeekAsync(groupId)` → populate `_assignments[0..6]`
  - Load meals via `MealRepository.GetAllAsync(groupId)` → populate dropdown options
  - Render 7-row table: day name (static array), `<select>` bound to `_assignments[i]`
- [x] 4.2 On `<select>` change:
  - Selected value `"0"` → `DayMealsRepository.ClearAsync(groupId, dayOfWeek)` → reload
  - Otherwise → `DayMealsRepository.UpsertAsync(groupId, dayOfWeek, mealId)` → reload

---

## 5. Unit Tests — `/week`

- [x] 5.1 Page renders 7 rows with correct day names
- [x] 5.2 Existing plan assignments pre-populate the selects correctly
- [x] 5.3 Selecting a meal calls `UpsertAsync` with correct `dayOfWeek` and `mealId`
- [x] 5.4 Selecting "— Not set —" calls `ClearAsync` with correct `dayOfWeek`

---

## 6. `/settings` — Settings Page

- [x] 6.1 Create `Components/Pages/Settings.razor` inheriting `AuthenticatedPageBase`:
  - `OnInitializedAsync`: load language + expiry threshold from `PreferenceRepository`
  - Render language `<select>` (EN/RU/PL) and expiry days `<input type="number" min="1" max="30">`
  - [Save] button: calls `PreferenceRepository.SetLanguageAsync` and `SetExpiryThresholdAsync`; shows `_saved = true` for 3 s via `Task.Delay(3000)` then `StateHasChanged()`
- [x] 6.2 If `SetExpiryThresholdAsync` does not exist on `PreferenceRepository`, add it (stores key `expiry_threshold_days`, follows existing preference-storage pattern)

---

## 7. Unit Tests — `/settings`

- [x] 7.1 Page loads and displays current preferences
- [x] 7.2 Clicking Save calls `SetLanguageAsync` and `SetExpiryThresholdAsync` with correct values
- [x] 7.3 Success message appears after save

---

## 8. `/ai` — AI Chat Page

- [x] 8.1 Create `Components/Pages/AiChat.razor` inheriting `AuthenticatedPageBase`:
  - Scrollable message list with user bubbles (right-aligned, indigo) and bot bubbles (left-aligned, gray)
  - Text input at bottom + Send button; Enter key submits via `@onkeydown`
  - On submit: append user message to `_messages`; set `_thinking = true`; disable input + show spinner on button
  - Await `AiQueryService.QueryAsync(Session.ChatId, _input)` → append bot response; clear input; `_thinking = false`
  - On exception: set `_error` banner; `_thinking = false`; don't clear user input
- [x] 8.2 If `OPENROUTER_API_KEY` is not set, `AiQueryService` will throw; ensure the error banner handles this gracefully with a user-friendly message

---

## 9. Unit Tests — `/ai`

- [x] 9.1 Submitting a message appends it to the list and calls `QueryAsync`
- [x] 9.2 Response from `QueryAsync` appended as bot message
- [x] 9.3 Exception from `QueryAsync` shows error banner; does not clear user input; `_thinking` reset to false

---

## 10. NavMenu Update

- [x] 10.1 Verify nav links for `/meals`, `/week`, `/settings`, `/ai` in `NavMenu.razor` point to the correct routes

---

## 11. Smoke Test (manual — do not mark complete without user confirmation)

- [ ] 11.1 Navigate to `/meals` → existing meals appear in sidebar; click one → ingredients and steps load in detail panel
- [ ] 11.2 Add a new meal → it appears in the sidebar; add an ingredient and a step → confirm via `/meals` command in Telegram
- [ ] 11.3 Rename a meal → name updates in sidebar and detail; confirm in Telegram
- [ ] 11.4 Reorder steps with ↑/↓ → order persists after page refresh
- [ ] 11.5 Delete a meal → removed from list; Telegram `/meals` no longer shows it
- [ ] 11.6 Navigate to `/week` → Mon–Sun grid renders; assign a meal to Wednesday via dropdown → confirm via Telegram `/week`
- [ ] 11.7 Clear Wednesday assignment → "— Not set —" shown; confirmed in Telegram
- [ ] 11.8 Navigate to `/settings` → current language shows; change to Polish → Save → subsequent Telegram bot replies come in Polish
- [ ] 11.9 Change expiry threshold → Save → success message shown; threshold persists after page refresh
- [ ] 11.10 Navigate to `/ai` → type a query → bot response appears in chat bubbles
- [ ] 11.11 Submit without `OPENROUTER_API_KEY` (remove from env) → error banner shown, page does not crash
- [ ] 11.12 All existing bot commands unaffected
