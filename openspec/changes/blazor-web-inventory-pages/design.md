## Prerequisite

`blazor-web-auth-scaffold` must be merged before implementing this change. All pages here inherit `AuthenticatedPageBase` and resolve `chatId` from `WebSessionStore`.

---

## Shared Conventions (all pages in this change)

- Inherit `AuthenticatedPageBase`; unauthenticated visits redirect to `/login`.
- Resolve `chatId` via `@inject WebSessionStore Session` → `Session.ChatId`.
- Group-scoped queries: call `await GroupRepository.GetOrCreateAsync(chatId)` first; use `group.Id` for repository calls.
- Loading state: `bool _loading = true` in `OnInitializedAsync`; spinner shown while true.
- Error banner: `string? _error`; rendered as `<div class="text-red-600 text-sm mt-2">@_error</div>`.
- After any mutation: call `await LoadDataAsync()` then `StateHasChanged()`.

---

## `/list` — Shopping List Page

### Data

- Read: `ShoppingItemRepository.GetActiveAsync(groupId)` — returns items not marked done.
- Add: `ShoppingListService.AddItemAsync(groupId, name, qty, expiryDate)`.
- Mark done: `ShoppingListService.MarkDoneAsync(itemId)`.
- Update: `ShoppingItemRepository.UpdateAsync(item)` (updates name, qty, expiry).
- Delete: `ShoppingItemRepository.DeleteAsync(itemId)`.

### UI Wireframe

```
┌─────────────────────────────────────────────────┐
│ Shopping List                    [+ Add item]   │
├──────────┬──────────┬──────────┬────────────────┤
│ ☐ Name   │ Qty      │ Expiry   │ Actions        │
├──────────┼──────────┼──────────┼────────────────┤
│ ☐ Milk   │ 2 L      │ May 20   │ ✎  ✕           │
│ ☐ Eggs   │ 12       │ —        │ ✎  ✕           │
│ ☑ Bread  │ 1 loaf   │ —        │ ✕  (grayed)    │
└──────────┴──────────┴──────────┴────────────────┘
```

- **Add item form**: slides open above the table on [+ Add item] click. Fields: Name (text), Qty (text), Expiry (date input). Submit calls `AddItemAsync`. Cancel collapses the form.
- **Inline edit**: clicking ✎ on a row replaces the three text cells with inputs; shows Save/Cancel buttons in the Actions column. Save calls `UpdateAsync`.
- **Mark done**: checkbox change calls `MarkDoneAsync`; row is styled `opacity-50 line-through`.
- **Delete**: ✕ toggles an inline confirmation (`Are you sure? Yes / No`) on the same row.

### Component State

```csharp
List<ShoppingItem> _items = [];
bool _showAddForm;
int? _editingId;
ShoppingItem? _editDraft;
int? _confirmDeleteId;
bool _loading = true;
string? _error;
```

---

## `/history` — Purchase History Page

### Data

- `PurchaseHistoryRepository.GetPageAsync(groupId, page, pageSize: 50, nameFilter, dateFrom, dateTo)`
  - Returns `(List<PurchaseHistoryEntry> Items, int TotalCount)`.
  - Filtering and pagination happen in SQL (`LIKE '%name%'`, `WHERE date >= dateFrom`).

### UI Wireframe

```
┌───────────────────────────────────────────────────────┐
│ Purchase History                                      │
│ [Date from ____] [Date to ____] [Item name ________] │
├──────────┬────────────────┬──────┬─────────┬─────────┤
│ Date     │ Item           │ Qty  │ Price   │ Shop    │
├──────────┼────────────────┼──────┼─────────┼─────────┤
│ May 15   │ Milk           │ 2 L  │ 3.20    │ Lidl    │
│ May 14   │ Eggs           │ 12   │ 2.50    │ —       │
├──────────┴────────────────┴──────┴─────────┴─────────┤
│                   ← Prev  Page 1 / 3  Next →         │
└───────────────────────────────────────────────────────┘
```

- Filter inputs are debounced 400 ms (`System.Timers.Timer` or `Task.Delay` in event handler).
- Pagination: `_page` int (1-based); Prev disabled when `_page == 1`; Next disabled when `_page * pageSize >= totalCount`.

### Component State

```csharp
List<PurchaseHistoryEntry> _items = [];
int _page = 1;
int _totalCount;
string _nameFilter = "";
DateOnly? _dateFrom;
DateOnly? _dateTo;
```

---

## `/prices` — Price Log Page

### Data

- `PriceLogRepository.GetAllAsync(groupId)` — returns all `PriceLogEntry` records.
- Group by `ItemName` client-side into `Dictionary<string, List<PriceLogEntry>>`.

### UI Wireframe

```
┌─────────────────────────────────────────────────────────┐
│ Price Log                                               │
│                                                         │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐        │
│  │ Milk       │  │ Eggs       │  │ Bread      │        │
│  │ Last: 3.20 │  │ Last: 2.50 │  │ Last: 1.80 │        │
│  │ ↑  ~~~~~~  │  │ →  ~~~~~~  │  │ ↓  ~~~~~~  │        │
│  └────────────┘  └────────────┘  └────────────┘        │
│                                                         │
│  [Milk expanded]                                        │
│  ├ May 15  3.20  Lidl                                   │
│  ├ May 10  3.00  Biedronka                              │
│  └ Apr 28  3.10  Lidl                                   │
└─────────────────────────────────────────────────────────┘
```

- **Trend arrow**: compare last two prices. Up ↑ (`text-red-500`) if last > prev; Down ↓ (`text-green-500`) if last < prev; Flat → (`text-gray-400`) if equal or only one data point.
- **Sparkline**: inline SVG, 60 × 24 px, polyline drawn from normalized prices. No external charting library.
- **Expand**: clicking a card toggles `_expandedItem`; shows a detail list below the card grid.

### Sparkline SVG Logic

```csharp
// Normalize prices to 0–24 range; map to SVG y-coordinates (inverted: low price = high on chart)
// Points: x = i * (60 / (count-1)), y = 24 - (normalized * 24)
// Render: <polyline points="..." stroke="indigo" fill="none" stroke-width="1.5"/>
```

### Component State

```csharp
Dictionary<string, List<PriceLogEntry>> _grouped = new();
string? _expandedItem;
```

---

## Repository Additions

`PurchaseHistoryRepository` needs a paginated/filtered query method if not already present:

```sql
SELECT * FROM PurchaseHistory
WHERE GroupId = @groupId
  AND (@nameFilter = '' OR ItemName LIKE '%' || @nameFilter || '%')
  AND (@dateFrom IS NULL OR Date >= @dateFrom)
  AND (@dateTo IS NULL OR Date <= @dateTo)
ORDER BY Date DESC
LIMIT @pageSize OFFSET @offset
```

If `GetPageAsync` already exists with a compatible signature, use it as-is.

---

## DI Additions to `ProductTrackerBot.Web/Program.cs`

```csharp
builder.Services.AddScoped<ShoppingItemRepository>();
builder.Services.AddScoped<PurchaseHistoryRepository>();
builder.Services.AddScoped<PriceLogRepository>();
builder.Services.AddScoped<ShoppingListService>();
```
