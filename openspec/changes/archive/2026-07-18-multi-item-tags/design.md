## Context

`product-groups` (shipped, not yet archived — see dependency note in `proposal.md`) added a single nullable `Category TEXT` column to `ShoppingItems` and `PurchaseHistory`, a follow-up capture dialog that resolves to exactly one value, and a `/list` filter keyed on that one column. Its own design doc recorded this as an explicit Non-Goal: "No multi-category tagging per item (one optional category per item, not a tag *set*)." Real usage now calls for that — an item like "Стиральный порошок" is plausibly both "Бытовая химия" and "Скидка" at once, and a single-column, mutually-exclusive value can't express two simultaneous, independent facts about one row. This forces a relational (many-to-many) model rather than another scalar column.

The existing capture-dialog shape (`PendingDialogService<TState>` + `IDialogMessageHandler` + suggestion/skip `ICallbackHandler`s) generalizes to multi-select by making the dialog state hold an accumulating *set* of choices instead of resolving on the first tap, plus one more terminal action ("Done") to close the set. This mirrors how a Telegram inline "multi-select checklist" is conventionally built when there's no native multi-select widget: toggle buttons that re-render with a ✓/blank state, plus an explicit confirm.

## Goals / Non-Goals

**Goals:**
- Let an item carry zero or more tags, each an independent free-text label.
- Preserve every existing single-category value as a tag during migration — no data loss.
- Multi-select tag capture: toggle suggestions on/off, add a brand-new tag by free text without losing already-toggled selections, confirm with "Готово".
- `/list` filtering by one or more tags at once (OR semantics — item matches if it has any active tag).
- Keep tag suggestion ranking sourced from `PurchaseHistory` (not the live, shrinking `ShoppingItems` table), same rationale as `product-groups` Decision 3.

**Non-Goals:**
- No tag management UI (rename/merge/delete a tag as a standalone entity) — renaming still means re-tagging items one at a time, same limitation `product-groups` accepted for categories.
- No AND-semantics multi-tag filtering in `/list` v1 (show items matching ALL active tags) — OR semantics only; AND filtering is a candidate follow-up if usage shows demand (see Open Questions).
- No tag capture for `/bought` — same exclusion `product-groups` made, unchanged here.
- No retroactive re-tagging suggestions or auto-merging of near-duplicate tag spellings ("Химия" vs "химия" is still two different tags unless the DB-level unique constraint's case-insensitivity treats them as one — see Decision 1).

## Decisions

### 1. Two new tables (`Tags`, `ItemTags`) plus a `PurchaseHistoryTags` join, not a JSON array column
Alternative considered: store tags as a JSON array TEXT column on `ShoppingItems` (e.g. `Tags: ["Молочное","Скидка"]`), matching the project's "variable-shape data as JSON TEXT via source-gen `JsonSerializerContext`" convention. Rejected: that convention is for genuinely variable-shape payloads (e.g. revert operations), not for a relational many-to-many that needs group-scoped uniqueness (`GetOrCreateAsync` semantics — the same tag name in the same group must resolve to the same row so frequency ranking and filtering work), efficient "does this item have tag X" queries, and frequency aggregation across `PurchaseHistory`. A JSON column can't be indexed or joined efficiently in raw ADO SQLite, and would force every filter/ranking query to deserialize and scan every row in Dart/C# instead of letting SQLite do it.

Schema:
```sql
CREATE TABLE IF NOT EXISTS Tags (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GroupId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    UNIQUE (GroupId, Name COLLATE NOCASE)
);

CREATE TABLE IF NOT EXISTS ItemTags (
    ItemId INTEGER NOT NULL,
    TagId INTEGER NOT NULL,
    PRIMARY KEY (ItemId, TagId)
);

CREATE TABLE IF NOT EXISTS PurchaseHistoryTags (
    PurchaseHistoryId INTEGER NOT NULL,
    TagId INTEGER NOT NULL,
    PRIMARY KEY (PurchaseHistoryId, TagId)
);
```
All three created via `CREATE TABLE IF NOT EXISTS` in `DatabaseInitializer`, per the existing additive pattern — no `ALTER TABLE` needed since these are brand-new tables, not new columns on existing ones. `Tags.Name` uniqueness is case-insensitive per group (`COLLATE NOCASE`), so "Химия" and "химия" typed by different users resolve to the same tag row — this is a deliberate tightening beyond `product-groups`' category column, which had no such collision handling since a plain string column has no natural place to enforce it.

### 2. One-time backfill migrates existing `Category` values into tags, run from `DatabaseInitializer`
On startup, after creating the new tables, `DatabaseInitializer` runs a one-time backfill (guarded by checking whether `ItemTags` is empty AND at least one `ShoppingItems.Category IS NOT NULL` row exists, so it only ever runs once and is a no-op on subsequent boots): for every `ShoppingItems` row with non-null `Category`, `GetOrCreateAsync(GroupId, Category)` the tag and insert into `ItemTags`; same for `PurchaseHistory.Category` → `PurchaseHistoryTags`. The `Category` columns themselves are left in place, untouched, and simply stop being written by new code — no `DROP COLUMN` (SQLite's `ALTER TABLE DROP COLUMN` support is version-gated and the project's migrations are additive-only by rule).

Alternative considered: a separate one-off console command/script for the backfill. Rejected — the project has no migration-runner infrastructure beyond `DatabaseInitializer.StartAsync`, and introducing one for a single backfill is disproportionate; an idempotent, startup-guarded backfill matches how every other schema change in this codebase self-migrates.

### 3. Multi-select capture dialog: toggle state lives in `TagCaptureDialogState`, resolved by an explicit "Done" callback
`TagCaptureDialogState` (replaces `CategoryCaptureDialogState`) carries: `ItemIds`, `ItemLabel`, `GroupId`, `TopTags` (suggestion pool, same `GetTopTagsAsync(groupId, limit)` shape as `GetTopCategoriesAsync`), and a new `SelectedTagNames` (`HashSet<string>`, mutable across the dialog's lifetime). Rendering: each suggestion button shows `✓ {name}` if in `SelectedTagNames`, plain `{name}` otherwise; tapping toggles membership and re-edits the prompt message in place (`EditMessageReplyMarkup`) — no new message per tap, matching how `/list` pagination already edits in place rather than resending. A free-text reply adds that text to `SelectedTagNames` (in addition to, not instead of, any toggled suggestions) and re-renders the same way. A new "Готово" button (`tag:done`) applies `SelectedTagNames` via `TagRepository.SetItemTagsAsync(itemIds, tagNames)` and clears the dialog; "Без категории"/skip (`tag:skip`) clears the dialog with an empty tag set, unchanged in spirit from `product-groups`.

Alternative considered: resolve immediately on each suggestion tap (current `product-groups` behavior) and let the user re-open the prompt to add a second tag. Rejected — there is no natural "re-open" affordance once the prompt's buttons are gone (Telegram doesn't let you tap a button on an already-dismissed message flow the same way twice without re-fetching state), and it would need N confirmation messages for N tags, which is worse UX than the toggle-then-confirm pattern used by virtually every multi-select in chat UIs.

Callback data stays within the 64-byte limit the same way `category:suggest:<index>` did: `tag:toggle:<index>` resolves `index` against `TopTags` freshly read from the still-pending `TagCaptureDialogState` (keyed by `(chatId, userId)`, not embedded in the callback), `tag:done` and `tag:skip` carry no parameters at all.

### 4. `/list` tag filter uses OR semantics via a set of active tag indices in callback data
Callback data becomes `list_filter:<groupChatId>:<tagIndexCsv>:<page>` where `tagIndexCsv` is a short comma-joined list of indices into the group's freshly-recomputed `GetDistinctTagsAsync(groupId)` result (e.g. `list_filter:12345:0,2:1`). Tapping an inactive tag button adds its index to the active set; tapping an active one removes it. `-1` (or an empty segment) denotes "no filter" / clears all, keeping the "Все" button's existing shape. This is a direct extension of `product-groups` Decision 4 (server-resolved index, not tag text, to stay under the byte limit) — the only change is that the callback carries a small set instead of a single index. Filtering matches an item if it has ANY tag whose index is in the active set (`ItemTags` join, `IN` over resolved `TagId`s).

Alternative considered: AND semantics (item must have every active tag). Rejected for v1 — OR semantics ("show me chemistry OR discount items") matches the original single-category filter's mental model more closely and is simpler to reason about when toggling multiple buttons; AND is a plausible follow-up (see Open Questions) but adds a second filter mode to design UI for (a toggle between OR/AND) that isn't justified without user demand yet.

## Risks / Trade-offs

- **[Risk]** Rollback after this ships is not fully symmetric: reverting the code deploy resumes reading the `Category` column, but tags added *after* this change shipped (and never written back to `Category`) would vanish from the old UI's perspective → **Mitigation**: called out explicitly in the proposal's rollback plan; acceptable because rollback here is a last-resort path, and no data is destructively lost (it's recoverable by forward-fixing, not by the rollback itself).
- **[Risk]** Case-insensitive per-group tag uniqueness (`COLLATE NOCASE`) only normalizes casing, not spelling variants ("Химия" vs "Бытовая химия") → **Mitigation**: unchanged limitation from `product-groups`, explicitly a Non-Goal; suggestion buttons remain the primary defense against fragmentation.
- **[Risk]** Toggle-based multi-select requires editing the prompt message on every tap (one Telegram API call per toggle) instead of resolving in one call → **Mitigation**: this matches the edit-in-place cost `/list` pagination already pays per navigation tap; no new rate-limit concern class introduced.
- **[Trade-off]** OR-only filtering in `/list` may under-serve users who specifically want an intersection view → **Mitigation**: flagged as an Open Question; ship OR first since it's the simpler, already-familiar mental model from the single-category filter it replaces.

## Migration Plan

1. Add `Tags`, `ItemTags`, `PurchaseHistoryTags` tables via `CREATE TABLE IF NOT EXISTS` in `DatabaseInitializer` (additive, no impact on existing columns).
2. On the same startup pass, run the one-time, idempotency-guarded backfill: existing `ShoppingItems.Category`/`PurchaseHistory.Category` values become tags, linked via the new join tables. `Category` columns are left in place, read-only from this point on.
3. Deploy handler/service changes (multi-select capture dialog, tag-aware `/list` filter) in the same release as the schema/backfill — there is no safe intermediate state where old single-category handlers run against the new tables, so this is a single coordinated deploy, not a staged rollout.
4. **Rollback**: revert the code deploy. The `Category` columns still hold their pre-migration values (never modified, only read from), so old code functions exactly as before rollback for any item untouched since. See the Risks section for the one asymmetry (tags added post-migration, pre-rollback aren't reflected back into `Category`).

## Open Questions

- Should `/list` eventually support AND semantics (intersection) as a second filter mode, once real usage shows people building overlapping tag sets? Deferred — ship OR-only first.
- Should there be a cap on tags per item (e.g. 10) to bound suggestion-button rendering and callback data size? Deferred until real usage data suggests it's needed; `SetItemTagsAsync` has no enforced cap in this design.
