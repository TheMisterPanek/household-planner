## Prerequisite

`blazor-web-auth-scaffold` must be merged before this change. `blazor-web-inventory-pages` is independent but should be merged first for a complete UI.

All pages here inherit `AuthenticatedPageBase` and resolve `chatId` via `WebSessionStore`.

---

## `/meals` — Meals Library Page

### Data

- List all meals: `MealRepository.GetAllAsync(groupId)`
- Get meal detail: `MealRepository.GetByIdAsync(mealId)` + `MealIngredientRepository.GetByMealAsync(mealId)` + `MealStepRepository.GetByMealAsync(mealId)`
- Add meal: `MealRepository.AddAsync(groupId, name)` → returns new `mealId`
- Rename meal: `MealRepository.UpdateNameAsync(mealId, name)`
- Delete meal: `MealRepository.DeleteAsync(mealId)` (cascades to ingredients + steps via existing FK logic)
- Add/update ingredient: `MealIngredientRepository.UpsertAsync(mealId, name, qty, unit)`
- Delete ingredient: `MealIngredientRepository.DeleteAsync(ingredientId)`
- Add step: `MealStepRepository.AddAsync(mealId, text, order)`
- Update step: `MealStepRepository.UpdateAsync(stepId, text)`
- Delete step: `MealStepRepository.DeleteAsync(stepId)`
- Reorder step: swap `Order` values of adjacent steps via two `UpdateAsync` calls

### UI Wireframe

```
┌─────────────────────────────────────────────────────┐
│ Meals                                               │
│ ┌─────────────┐  ┌──────────────────────────────┐  │
│ │ Pasta       │  │ Pasta                 [Rename]│  │
│ │ Pizza       │  │                               │  │
│ │ Soup        │  │ Ingredients          [+ Add]  │  │
│ │             │  │ ┌──────────┬──────┬──────────┐│  │
│ │ [+ New meal]│  │ │Flour     │500g  │  ✕       ││  │
│ └─────────────┘  │ │Eggs      │2     │  ✕       ││  │
│                  │ └──────────┴──────┴──────────┘│  │
│                  │                               │  │
│                  │ Steps                [+ Add]  │  │
│                  │ 1. Boil water    ↑ ↓ ✎ ✕     │  │
│                  │ 2. Add pasta     ↑ ↓ ✎ ✕     │  │
│                  └──────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### Component State

```csharp
List<Meal> _meals = [];
Meal? _selected;
List<MealIngredient> _ingredients = [];
List<MealStep> _steps = [];
bool _editingName;
string _nameDraft = "";
bool _showAddIngredient;
MealIngredient _ingredientDraft = new();
bool _showAddStep;
string _stepDraft = "";
int? _editingStepId;
string _stepEditDraft = "";
```

---

## `/week` — Weekly Meal Plan Page

### Data

- Load plan: `DayMealsRepository.GetWeekAsync(groupId)` → `List<DayMealEntry>` (DayOfWeek 1–7)
- Load meal list: `MealRepository.GetAllAsync(groupId)` (for dropdown options)
- Assign: `DayMealsRepository.UpsertAsync(groupId, dayOfWeek, mealId)`
- Clear: `DayMealsRepository.ClearAsync(groupId, dayOfWeek)`

### UI Wireframe

```
┌────────────────────────────────────┐
│ Weekly Meal Plan                   │
│                                    │
│  Mon  │ Pasta          ▼           │
│  Tue  │ —              ▼           │
│  Wed  │ Pizza          ▼           │
│  Thu  │ —              ▼           │
│  Fri  │ Soup           ▼           │
│  Sat  │ —              ▼           │
│  Sun  │ —              ▼           │
└────────────────────────────────────┘
```

Each row: day name (fixed, from a static array `Mon Tue Wed Thu Fri Sat Sun`), and a `<select>` element populated with all meals + a "— Not set —" option at the top.

On `<select>` change event:
- If selected value is `"0"` (not set) → `ClearAsync`
- Otherwise → `UpsertAsync` with the selected `mealId`

No explicit Save button — changes apply immediately on selection change.

### Component State

```csharp
// Index 0=Mon … 6=Sun; null means unset
int?[] _assignments = new int?[7];
List<Meal> _meals = [];
```

---

## `/settings` — Settings Page

### Data

- Read language: `PreferenceRepository.GetLanguageAsync(chatId)` (returns `"en"` / `"ru"` / `"pl"`)
- Set language: `PreferenceRepository.SetLanguageAsync(chatId, lang)`
- Read expiry threshold: `PreferenceRepository.GetExpiryThresholdAsync(chatId)` (returns days as `int`)
- Set expiry threshold: `PreferenceRepository.SetExpiryThresholdAsync(chatId, days)` (if method doesn't exist, store as a generic `preference` key via existing pattern)

### UI Wireframe

```
┌───────────────────────────────────┐
│ Settings                          │
│                                   │
│ Language                          │
│ [EN ▼]                            │
│                                   │
│ Expiry warning (days ahead)       │
│ [3  ]                             │
│                                   │
│               [Save]              │
│                                   │
│ ✓ Settings saved.   (after save)  │
└───────────────────────────────────┘
```

- Language select: options `EN`, `RU`, `PL`.
- Expiry days: `<input type="number" min="1" max="30">`.
- Success message shown for 3 s after save (using `Task.Delay(3000)` then `StateHasChanged()`).

### Component State

```csharp
string _language = "en";
int _expiryDays = 3;
bool _saved;
string? _error;
```

---

## `/ai` — AI Chat Page

### Data

- Query: `AiQueryService.QueryAsync(chatId, userMessage)` → `string` response
- No persistence — chat history lives in component state only

### UI Wireframe

```
┌───────────────────────────────────────┐
│ AI Query                              │
│ ┌─────────────────────────────────┐  │
│ │ You: What should I buy?         │  │ ← right-aligned bubble
│ │                                 │  │
│ │ Bot: Based on your list, ...    │  │ ← left-aligned bubble
│ └─────────────────────────────────┘  │
│                                       │
│ [Ask something...           ] [Send]  │
└───────────────────────────────────────┘
```

- User bubbles: `bg-indigo-600 text-white self-end rounded-xl rounded-br-sm px-4 py-2 max-w-xs`.
- Bot bubbles: `bg-gray-100 text-gray-800 self-start rounded-xl rounded-bl-sm px-4 py-2 max-w-sm`.
- While waiting for response: spinner replaces Send button; input disabled.
- If `AiQueryService` throws (e.g., missing API key): show `_error` banner; don't crash the page.

### Component State

```csharp
record ChatMessage(string Role, string Text);   // Role: "user" | "bot"
List<ChatMessage> _messages = [];
string _input = "";
bool _thinking;
string? _error;
```

---

## DI Additions to `ProductTrackerBot.Web/Program.cs`

```csharp
builder.Services.AddScoped<MealRepository>();
builder.Services.AddScoped<MealIngredientRepository>();
builder.Services.AddScoped<MealStepRepository>();
builder.Services.AddScoped<DayMealsRepository>();
builder.Services.AddScoped<MealMergeService>();
builder.Services.AddScoped<IAiQueryService, AiQueryService>();
builder.Services.AddHttpClient<IOpenRouterClient, OpenRouterClient>();
```

`AiQueryService` already depends on `IOpenRouterClient` and `ConversationHistoryService`; register those too if not already present from prior changes.
