## Prerequisites

- `blazor-web-auth-scaffold` merged and deployed
- `AuthenticatedPageBase`, `WebSessionStore`, layout shell, and DI wiring all in place

---

## 1. Repository: `PurchaseHistoryRepository` — Paginated Query

- [ ] 1.1 Check if `GetPageAsync(groupId, page, pageSize, nameFilter, dateFrom, dateTo)` already exists
- [ ] 1.2 If not, add the method:
  - SQL: `SELECT ... WHERE GroupId = @groupId AND (@nameFilter = '' OR ItemName LIKE '%'||@nameFilter||'%') AND ... ORDER BY Date DESC LIMIT @pageSize OFFSET @offset`
  - Also query total count (`SELECT COUNT(*)` with same filters) for pagination display
  - Returns `(List<PurchaseHistoryEntry> Items, int TotalCount)`

---

## 2. DI Registration in `ProductTrackerBot.Web/Program.cs`

- [ ] 2.1 Register `ShoppingItemRepository` as `AddScoped`
- [ ] 2.2 Register `PurchaseHistoryRepository` as `AddScoped`
- [ ] 2.3 Register `PriceLogRepository` as `AddScoped`
- [ ] 2.4 Register `ShoppingListService` as `AddScoped`

---

## 3. `/list` — Shopping List Page

- [ ] 3.1 Create `Components/Pages/ShoppingList.razor` inheriting `AuthenticatedPageBase`:
  - `OnInitializedAsync`: resolves `groupId` via `GroupRepository.GetOrCreateAsync(Session.ChatId)`; loads items via `ShoppingItemRepository.GetActiveAsync(groupId)`
  - Renders table with columns: checkbox, Name, Qty, Expiry, Actions (✎ ✕)
  - Done items shown at the bottom with `opacity-50 line-through`
- [ ] 3.2 Add item form (collapsible):
  - [+ Add item] button at top-right toggles `_showAddForm`
  - Form fields: Name (required), Qty (text), Expiry (date)
  - Submit → `ShoppingListService.AddItemAsync(groupId, name, qty, expiryDate)` → reload; cancel → collapse
- [ ] 3.3 Inline edit:
  - ✎ button sets `_editingId` and `_editDraft` (clone of item)
  - Affected row renders inputs for Name, Qty, Expiry; Save/Cancel in Actions column
  - Save → `ShoppingItemRepository.UpdateAsync(_editDraft)` → reload → clear `_editingId`
- [ ] 3.4 Mark done:
  - Checkbox `@onchange` → `ShoppingListService.MarkDoneAsync(item.Id)` → reload
- [ ] 3.5 Delete with inline confirmation:
  - ✕ sets `_confirmDeleteId = item.Id`; row shows "Are you sure? [Yes] [No]"
  - Yes → `ShoppingItemRepository.DeleteAsync(id)` → reload; No → clear `_confirmDeleteId`

---

## 4. Unit Tests — `ShoppingList.razor`

*Blazor component tests using bUnit or integration-style assertions against service mocks.*

- [ ] 4.1 Page redirects to `/login` when `WebSessionStore.IsAuthenticated` is false
- [ ] 4.2 Page renders item rows from `ShoppingItemRepository.GetActiveAsync`
- [ ] 4.3 Adding an item calls `ShoppingListService.AddItemAsync` with correct parameters
- [ ] 4.4 Clicking ✎ on a row renders input fields for that row only
- [ ] 4.5 Save on edit calls `ShoppingItemRepository.UpdateAsync` with modified values
- [ ] 4.6 Clicking ✕ shows inline confirmation; clicking Yes calls `DeleteAsync`
- [ ] 4.7 Checking the checkbox calls `MarkDoneAsync`

---

## 5. `/history` — Purchase History Page

- [ ] 5.1 Create `Components/Pages/History.razor` inheriting `AuthenticatedPageBase`:
  - `OnInitializedAsync`: loads page 1 via `GetPageAsync(groupId, 1, 50, "", null, null)`
  - Renders filter bar (date-from, date-to, name-filter inputs) above a table
  - Table columns: Date, Item, Qty, Price, Shop
  - Pagination row: `← Prev  Page N / M  Next →` with disabled state at bounds
- [ ] 5.2 Filter behaviour:
  - Each filter input has `@oninput` that triggers a debounced reload (400 ms via `Task.Delay`)
  - On filter change: reset `_page = 1`; reload
- [ ] 5.3 Pagination:
  - Prev → `_page--` → reload; disabled when `_page == 1`
  - Next → `_page++` → reload; disabled when `_page * 50 >= _totalCount`

---

## 6. Unit Tests — `History.razor`

- [ ] 6.1 Renders table rows from `GetPageAsync` result
- [ ] 6.2 Prev button disabled on page 1; Next button disabled when on last page
- [ ] 6.3 Changing name filter triggers reload with `_page` reset to 1

---

## 7. `/prices` — Price Log Page

- [ ] 7.1 Create `Components/Pages/Prices.razor` inheriting `AuthenticatedPageBase`:
  - `OnInitializedAsync`: `PriceLogRepository.GetAllAsync(groupId)` → group by `ItemName` into `_grouped`
  - Renders 3-column card grid; each card: item name, last price, trend arrow, inline SVG sparkline
- [ ] 7.2 Trend arrow logic:
  - Compare last two prices in sorted list; ↑ red if higher, ↓ green if lower, → gray if equal/single
- [ ] 7.3 SVG sparkline:
  - 60 × 24 px `<svg>`; normalize price values to [0, 24]; render `<polyline>` with `stroke="indigo"` `fill="none"`
  - If only one data point, render a dot instead of a line
- [ ] 7.4 Click to expand:
  - Clicking a card sets `_expandedItem` to that item name; renders a detail list below the grid
  - Clicking the same card again collapses; clicking another expands that one

---

## 8. Unit Tests — `Prices.razor`

- [ ] 8.1 Groups price entries by item name correctly
- [ ] 8.2 Trend arrow is ↑ when last price > previous; ↓ when lower; → when equal or single entry
- [ ] 8.3 Clicking a card sets `_expandedItem`; clicking again clears it

---

## 9. NavMenu Update

- [ ] 9.1 Ensure nav links for `/list`, `/history`, `/prices` in `NavMenu.razor` point to the correct routes (they were placeholder links in the scaffold — verify they now load the real components)

---

## 10. Smoke Test (manual — do not mark complete without user confirmation)

- [ ] 10.1 Navigate to `/list` → shopping list loads with existing items from DB
- [ ] 10.2 Click [+ Add item] → form opens; fill in Name + Qty; submit → item appears in list; bot `/list` also shows it
- [ ] 10.3 Check checkbox on an item → item grays out; `/list` in Telegram confirms it's done
- [ ] 10.4 Click ✎ on an item → row becomes editable; change name; Save → updated name persists after page refresh
- [ ] 10.5 Click ✕ → confirmation appears; click Yes → item gone; confirmed via Telegram `/list`
- [ ] 10.6 Navigate to `/history` → purchase history table loads; filter by item name → rows narrow correctly; Prev/Next pagination works
- [ ] 10.7 Navigate to `/prices` → price cards appear; trend arrows and sparklines render; clicking a card shows detail list
- [ ] 10.8 All existing bot commands unaffected
