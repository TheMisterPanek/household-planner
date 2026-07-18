## MODIFIED Requirements

### Requirement: View shared shopping list via /list command
The system SHALL post or edit a persistent shopping list message in the group chat showing all current items with inline action buttons. When the group has at least one item carrying one or more tags, the message SHALL include an additional row of tag filter buttons (per the `item-tags` capability) below the item rows.

#### Scenario: List has items
- **WHEN** a group member sends `/list`
- **THEN** the bot posts (or edits the existing) list message with header "🛒 Список покупок:" and one inline button row per item: `[✓ Name qty]` and `[✗ Убрать]`

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

#### Scenario: List with no tagged items shows no filter row
- **WHEN** a group member sends `/list` and no item in the group carries any tag
- **THEN** the list message renders exactly as before, with no tag filter button row

#### Scenario: List with tagged items shows a filter row
- **WHEN** a group member sends `/list` and at least one item carries a tag
- **THEN** the list message includes a row of tag filter buttons (up to 5, alphabetical) below the item rows, in addition to the unfiltered item list and existing action buttons

---

## ADDED Requirements

### Requirement: Filter the shopping list by one or more tags
The system SHALL let a user tap one or more tag filter buttons on the `/list` message to re-render the message showing only items carrying at least one of the currently-active tags (OR semantics), using the same pagination as the unfiltered view. Tapping an active tag button again deactivates it. A "Все" (All) button SHALL be shown when any filter is active, clearing all active tags and returning to the unfiltered view.

#### Scenario: User taps a tag filter button
- **WHEN** a user taps the "Бытовая химия" filter button on the list message
- **THEN** the message is edited to show only items carrying the "Бытовая химия" tag, paginated the same way as the unfiltered list, with "Бытовая химия" now shown as active and an "Все" button available to clear the filter

#### Scenario: User taps a second tag filter button while one is already active
- **WHEN** "Бытовая химия" is already active and the user taps the "Скидка" filter button
- **THEN** the message is edited to show items carrying "Бытовая химия" OR "Скидка" (union, not intersection), with both buttons now shown as active

#### Scenario: User taps an already-active tag filter button to deactivate it
- **WHEN** "Бытовая химия" and "Скидка" are both active and the user taps "Бытовая химия" again
- **THEN** "Бытовая химия" is deactivated, and the message is edited to show only items carrying "Скидка"

#### Scenario: User taps "Все" to clear all active filters
- **WHEN** one or more tag filters are active and the user taps the "Все" button
- **THEN** the message is edited back to the unfiltered, paginated view of all items, and no tag buttons remain marked active

#### Scenario: Filtered view respects pagination for large tag sets
- **WHEN** one or more tag filters are active and more than 10 items match the active set
- **THEN** the filtered view paginates at 10 items per page, using the same next/prev navigation as the unfiltered list

#### Scenario: An active tag no longer exists at tap time
- **WHEN** a user taps "Все" or navigates pagination while one of the previously active tags no longer has any items (e.g. all were removed or re-tagged since the message was rendered)
- **THEN** the bot drops the stale tag from the active set and falls back to the unfiltered "All" view if no active tags remain, rather than showing an empty or error state
