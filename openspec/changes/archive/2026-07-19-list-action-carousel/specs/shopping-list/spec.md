## MODIFIED Requirements

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat showing all current items with inline action buttons. When the group has at least one item with a non-null `Category`, the message SHALL include additional rows of category filter buttons, wrapped to at most 2 buttons per row. The item-pagination row and the tag-pagination row (when present) SHALL each render an inline page indicator button (`[n/N]`) between the Previous/Next buttons, providing visual context in place of a separate spacer row.

#### Scenario: List has items
- **WHEN** a group member sends `/list`
- **THEN** the bot posts (or edits the existing) list message with header "🛒 Список покупок:" and one inline button row per item: `[✓ Name qty]` and `[✕]`

#### Scenario: List is empty
- **WHEN** a group member sends `/list` and no items exist
- **THEN** the bot posts "Список покупок пуст" with no buttons

#### Scenario: Subsequent /list call and list message is still last
- **WHEN** `/list` is called, a `ListMessageId` is already saved, and no messages were sent after it
- **THEN** the bot edits the existing message in place

#### Scenario: Subsequent /list call and chat has newer messages
- **WHEN** `/list` is called, a `ListMessageId` is already saved, but newer messages exist after it in the chat
- **THEN** the bot posts a new list message and saves the new `MessageId` to the group

#### Scenario: Edit limit exceeded
- **WHEN** editing the list message returns a Telegram 400 error (48h limit)
- **THEN** the bot posts a new message and saves the new `MessageId` to the group

#### Scenario: List with no categorized items shows no filter row
- **WHEN** a group member sends `/list` and no item in the group has a `Category` set
- **THEN** the list message renders with no category filter rows

#### Scenario: List with categorized items shows wrapped filter rows
- **WHEN** a group member sends `/list` and at least one item has a `Category` set, with 5 categories plus "Все" active (6 buttons total)
- **THEN** the filter buttons render across 3 rows of at most 2 buttons each, rather than one crowded row

#### Scenario: Filter row paginates when there are more than 6 tags
- **WHEN** a group has 10 distinct tags
- **THEN** the filter block shows tag page 1 (tags 1-6, wrapped 2 per row) followed by a `[1/2] [Next →]` row; tags 7-10 are reachable by paging, not permanently hidden as they are today

#### Scenario: Tag page navigation preserves the active filter and item page
- **WHEN** a user on tag page 1 taps `[Next →]` in the tag-pagination row
- **THEN** the bot re-renders the list showing tag page 2, with the current item page and any active tag filter selection unchanged

#### Scenario: Toggling a tag preserves the current tag page
- **WHEN** a user on tag page 2 taps a tag button to add/remove it from the active filter
- **THEN** the bot re-renders with the updated filter applied and the tag block still showing tag page 2, not reset to tag page 1

#### Scenario: Item pagination resets the tag page
- **WHEN** a user taps `[← Previous]`/`[Next →]` on the item-action pagination row while the tag block is showing tag page 2
- **THEN** the bot re-renders the requested item page with the tag block reset to tag page 1

#### Scenario: All Items and Cancel remain reachable from any tag page
- **WHEN** a filter is active and the tag block is showing any tag page (not just the last one)
- **THEN** the "All Items" (clear filter) button and the "Cancel" button render as fixed rows immediately below the tag-page block, regardless of which tag page is displayed

#### Scenario: Item pagination row shows a page indicator
- **WHEN** the list has more than one carousel page of items (see `list-pagination`)
- **THEN** the pagination row renders `[← Previous] [n/N] [Next →]`, omitting Previous on the first page and Next on the last page

#### Scenario: No pagination row when there is only one page
- **WHEN** the list has only one carousel page (item count ≤ `ActionPageSize`)
- **THEN** no pagination row is rendered — only the item action rows, any filter rows, and the final Cancel row

---

## ADDED Requirements

### Requirement: Page-indicator buttons are visually present but functionally inert
The system SHALL render the `[n/N]` page-indicator button (on both the item-pagination and tag-pagination rows) as inert. Tapping it SHALL acknowledge the Telegram callback (clearing the client-side loading indicator) and SHALL NOT change any list state, item state, or dialog state.

#### Scenario: Tapping the page indicator does nothing
- **WHEN** a user taps the `[n/N]` page-indicator button on the list message
- **THEN** the callback query is answered (no loading spinner persists) and the list message, item state, and any pending dialogs are left completely unchanged
