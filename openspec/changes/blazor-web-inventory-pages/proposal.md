## Why

With the Blazor scaffold and authentication from `blazor-web-auth-scaffold` in place, this change delivers the three highest-value everyday pages: Shopping List (full CRUD, the bot's core feature), Purchase History (browseable audit trail), and Prices (price-over-time per item). These cover the most frequent household use cases and validate the shared layout and repository wiring before the more complex meal/AI pages.

## What Changes

### `/list` — Shopping List

Full CRUD mirror of the bot's shopping-list capabilities:
- View all active items (name, qty, expiry).
- Add item via inline form.
- Mark done via checkbox.
- Inline edit (name / qty / expiry) — click row to activate.
- Delete with an inline confirmation popover (no modal dialog).

### `/history` — Purchase History

Paginated, filterable table backed by `PurchaseHistoryRepository`:
- 50 rows/page, Prev/Next pagination.
- Filter bar: date-from, date-to, item-name substring.

### `/prices` — Price Log

Per-item card grid backed by `PriceLogRepository`:
- Each card: item name, last price, trend arrow (↑↓→), inline SVG sparkline.
- Clicking a card expands an inline price-over-time detail list.

## Capabilities

### New Capabilities

- `web-shopping-list`: Full CRUD for shopping items from a browser.
- `web-purchase-history`: Browse and filter purchase history with pagination.
- `web-price-log`: View price history per item with sparklines.

## Out of Scope

- Meals, Week Plan, Settings, AI — delivered in `blazor-web-advanced-pages`.
- Real-time push for list changes (no SignalR in this iteration).

## Impact

- **Code**: 3 new Razor page components inside `ProductTrackerBot.Web`; no changes to `ProductTrackerBot` repositories or services.
- **APIs**: None new — pages call repositories directly via DI.
- **Dependencies**: No new NuGet packages.
- **Systems**: Read/write to existing SQLite tables (`ShoppingItems`, `PurchaseHistory`, `PriceLog`).
- **Compatibility**: AOT not applicable to the web project; shared repositories stay AOT-safe.

## Rollback Plan

Delete `ShoppingList.razor`, `History.razor`, `Prices.razor` from `ProductTrackerBot.Web/Components/Pages/`. Remove nav links from `NavMenu.razor`. No bot-side or DB changes to revert.

## Affected Teams

Single-user/household deployment. No shared infrastructure impact.

## Cross-Cutting Notes

- Pages inherit `AuthenticatedPageBase` (from `blazor-web-auth-scaffold`); unauthenticated requests redirect to `/login`.
- All repository calls use `chatId` resolved from the session cookie claim.
- Group-scoped data (shopping items) goes through `GroupRepository.GetOrCreateAsync(chatId)` before querying group-keyed tables.
- No new localization keys needed — web UI uses English strings rendered directly in Razor markup (not via `ILocalizer`, which is Telegram-chat-scoped).
- No `IHistoryRepository.RecordAsync` needed for web reads; mark-done and delete mutations already recorded by the shared service layer.
