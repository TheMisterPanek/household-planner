## Context

The current `/buy` command (implemented in `BuyCommandHandler`) accepts inline input like `/buy Молоко 2л` or opens a two-step dialog. The inline path extracts the last token as quantity and all preceding tokens as the item name. This design must maintain backward compatibility while adding comma-separated parsing.

## Goals / Non-Goals

**Goals:**
- Support `/buy item1, item2, item3` syntax for bulk item entry
- Parse quantity-suffixed items: `/buy milk 1L, eggs 6` → two items with respective quantities
- Maintain backward compatibility: `/buy milk 2L` still works as before
- Record history for all items added in a bulk operation
- Localize confirmation messages for bulk additions

**Non-Goals:**
- Dialog flow changes (no CSV support in the two-step dialog)
- Nested parsing (e.g., "item (note)" syntax within CSV)
- Special characters or escaping logic
- Pagination or truncation of bulk additions

## Decisions

**Decision 1: Parsing Location**
- **Chosen**: Split by comma in `BuyCommandHandler.Handle()` before delegating to `ShoppingListService`
- **Rationale**: Keeps business logic in the service; handler responsible for input normalization
- **Alternative**: Move parsing to `ShoppingListService.AddItemAsync()` — less clean separation, reusable by other handlers if needed later

**Decision 2: Quantity Extraction**
- **Chosen**: Apply existing quantity-parsing logic (last token = quantity) to each comma-separated item independently
- **Rationale**: Reuses proven logic, consistent user experience across inline and CSV paths
- **Example**: `/buy milk 1L, eggs 6, bread` → items: (milk, 1L), (eggs, 6), (bread, null)

**Decision 3: Confirmation Message**
- **Chosen**: 
  - Single item: Existing "Иван добавил(а) Молоко 2л"
  - Multiple items: New template "Иван добавил(а) N товара/товаров: item1, item2, item3"
- **Localization key**: `shop.bulk-added` with placeholders `{Count}` and `{Items}`
- **Rationale**: Acknowledges bulk operation, summarizes what was added

**Decision 4: History Recording**
- **Chosen**: Record one history entry per item added (not one bulk entry)
- **Rationale**: Maintains audit trail granularity; each item's add is a discrete action; simplifies downstream queries (e.g., "who added milk?")
- **Alternative**: Single bulk-add entry with JSON array of items — harder to query, overkill for audit trail

**Decision 5: Whitespace Handling**
- **Chosen**: Trim leading/trailing whitespace from each comma-separated segment after split
- **Rationale**: Users naturally add spaces: `/buy milk, eggs, bread` should work
- **Implementation**: `input.Split(',').Select(s => s.Trim()).ToList()`

## Risks / Trade-offs

**[Risk] Very large bulk operations slow down handler**
- Scenario: User sends `/buy item1, item2, ... item50`
- Mitigation: Limit to 20 items per `/buy` command; silently truncate or reply with "max 20 items per command"; log at warning level

**[Risk] Localization key missing for new bulk-added template**
- Mitigation: Pre-create `shop.bulk-added` key in all three language files (en, ru, pl) before release

**[Risk] History repository write fails for one item out of N**
- Mitigation: Existing pattern: catch exception, log at Warning, continue; user-facing confirmation still sent; partial history is better than lost confirmation

**[Trade-off] One history entry per item vs. one bulk entry**
- Chosen approach (one per item) is simpler but generates more history rows
- Bulk entry would save storage but complicates querying

## Migration Plan

**Deployment:**
1. Add `shop.bulk-added` localization keys to all language files
2. Deploy updated `BuyCommandHandler` and `ShoppingListService` (no schema changes)
3. No data migration needed — existing items unaffected

**Rollback:**
1. Revert handler to treat entire input as single item name
2. No data cleanup required

## Open Questions

- Should we enforce a maximum number of items per bulk `/buy` command? (Proposed: 20)
- Should the confirmation list items in the language of the group or always in English? (Recommendation: use group language, same as list output)
