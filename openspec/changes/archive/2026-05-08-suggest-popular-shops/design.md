## Context

Currently, when `PriceCaptureStepHandler` processes step 1 (asking for store name), it simply sends a message with a [Skip] button and waits for free-text input. Users must type the store name from scratch each time, even if they've shopped at the same place many times.

The `PurchaseHistory` table already stores `StoreName` for all prior purchases, keyed by `(GroupId, UserId, PurchasedAt)`. We can use this historical data to suggest the user's most-frequently-visited stores as inline buttons.

## Goals / Non-Goals

**Goals:**
- Reduce user friction during price capture by offering 1-click selection of frequently used shops
- Minimize database queries (fetch top shops once per dialog start, not per suggestion)
- Keep suggested shop list short (5 items max) to avoid cluttering the Telegram UI
- Support mixed input: users can tap a suggestion OR type a custom shop name
- Maintain per-user scoping (each user in a group sees only their own shopping history)

**Non-Goals:**
- Cross-group shop suggestions (shops are user-specific, not group-wide)
- Shop typo correction or fuzzy matching
- Ranking by recency vs. frequency (use frequency only for simplicity)
- Persisting shop suggestions in the database (compute on-demand)

## Decisions

**Decision 1: Query top shops at step 1 entry, store in `PriceCaptureDialogState`**
- When `PriceCaptureStepHandler` handles step 1, call `PurchaseHistoryRepository.GetTopShopsAsync(groupId, userId, 5)` and store the list in the dialog state
- Rationale: Fetch once per dialog, avoid repeated DB queries. The list is immutable within a single dialog instance
- Alternative: Query in real-time for each button render — rejected, adds DB pressure

**Decision 2: Display as inline buttons in a single row (or wrap to 2 rows if needed)**
- Add shop buttons before the [Skip] button in a 1-2 button-per-row layout
- Rationale: Telegram inline keyboards are flexible; stack buttons naturally to fit screen width
- Alternative: Show as a numbered list in message text — rejected, less discoverable and requires manual input

**Decision 3: `GetTopShopsAsync` returns non-null `StoreName` values only, ordered by frequency DESC, then by most-recent `PurchasedAt` DESC**
- Rationale: Skip null `StoreName` entries (incomplete data); frequency ranking; tiebreak by recency
- No need for a separate "popularity score" table — compute on query

**Decision 4: Reuse existing `PriceCaptureDialogState` class; add `TopShops: List<string>?` field**
- Rationale: State is already serialized in-memory; no DB schema change needed
- Alternative: Compute shops dynamically in the message builder — rejected, couples dialog logic to rendering

**Decision 5: User-typed custom shop name overrides suggestions (standard text input flow)**
- If user types anything, use that as `StoreName`, regardless of suggestions shown
- Rationale: No special handling needed; free text always takes precedence

## Risks / Trade-offs

**[Risk]** If a user has no prior purchases with a `StoreName` set, no suggestions appear
- **Mitigation**: Fallback gracefully to [Skip] button and free-text input only. This is the common case for new users

**[Risk]** Five buttons + [Skip] button (6 total) may wrap awkwardly on narrow mobile screens
- **Mitigation**: Keep button labels short (shop names as-is). Telegram handles layout; users can scroll inline keyboards. If a single shop name is very long (>20 chars), truncate at query time

**[Risk]** Stale suggestions if user adds a new favorite shop mid-session in another group
- **Mitigation**: Acceptable — suggestions are query-time computed, next dialog will show updated list

**[Trade-off]** Frequency ranking is simple (count of prior purchases) but doesn't weight by time. Recently added shops may never surface if user is a long-time customer of others
- **Mitigation**: Acceptable for MVP; recency weighting can be added later if needed

## Migration Plan

1. Add `TopShops: List<string>?` field to `PriceCaptureDialogState`
2. Implement `PurchaseHistoryRepository.GetTopShopsAsync(int groupId, long userId, int limit)`
3. Update `PriceCaptureStepHandler.HandleStep1Async` to fetch and store top shops, render as buttons
4. No database migrations needed (no schema changes)
5. Rollback: Remove `TopShops` field from state, revert `HandleStep1Async` to original logic

## Open Questions

- Should we show store count/frequency label on buttons (e.g., "Carrefour (8 times)"), or just the shop name? → Suggest: shop name only (cleaner, fits 64-byte limit better)
- What truncation length for very long shop names? → Suggest: 30 characters, ellipsis at end
- Should we refresh top shops if user provides a new shop name in the dialog? → Suggest: No, keep suggestions static per dialog
