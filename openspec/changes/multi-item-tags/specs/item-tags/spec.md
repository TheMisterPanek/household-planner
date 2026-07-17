## ADDED Requirements

### Requirement: Prompt for tags after an item is added
The system SHALL start a tag-capture follow-up immediately after an item (or, for bulk add, a batch of items) is successfully persisted via `/buy`, sending a message asking which tags to file it under, with toggleable inline buttons for the group's suggested tags (per "Suggest frequently-used tags") plus "Без категории" (skip) and "Готово" (Done) buttons. This applies to inline single-item add, the `/buy` dialog (both the two-step flow and one-line re-entry), the skip-quantity path, and bulk comma-separated add.

#### Scenario: Tag prompt follows a single-item add
- **WHEN** an item is successfully added via any single-item `/buy` path (inline, dialog, or skip-quantity) and the "Иван добавил(а) X" confirmation is sent
- **THEN** the bot sends a separate follow-up message "В какие группы добавить X?" with toggleable tag suggestion buttons, a "Без категории" button, and a "Готово" button

#### Scenario: Tag prompt follows a bulk add, asked once for the whole batch
- **WHEN** multiple items are added via comma-separated bulk `/buy` and the bulk confirmation is sent
- **THEN** the bot sends one follow-up message asking which tags to file all of the newly-added items under, not one prompt per item

#### Scenario: Tag prompt follows an item edit
- **WHEN** a user saves an edit to an item's name/quantity via the one-line edit flow
- **THEN** the bot sends the same tag follow-up prompt for that single item after the edit is saved, pre-selecting any tags already on the item, so tags can be added, removed, or left as-is

#### Scenario: New tag-capture prompt discards a still-pending one
- **WHEN** a user has an unanswered tag prompt pending and adds another item (or edits another item) before answering it
- **THEN** the new prompt replaces the old pending one; the earlier item is left with whatever tags it already had (none, if newly added)

---

### Requirement: Toggle suggested tags and add free-text tags before confirming
The system SHALL let a user build up a set of tags for the pending item(s) by tapping any number of suggested tag buttons (each tap toggles that tag on or off, re-rendering the button with a ✓ prefix when selected) and/or replying with free text, which adds the typed text as an additional tag without clearing any already-toggled selections. The set is only applied when the user taps "Готово"; tapping "Без категории" clears the dialog with no tags applied.

#### Scenario: User toggles a suggested tag on
- **WHEN** a user taps a suggested tag button (e.g. "Бытовая химия") on the prompt
- **THEN** the button re-renders as "✓ Бытовая химия" and the tag is added to the pending selection, but the item(s) are not yet updated and the prompt remains open

#### Scenario: User toggles a suggested tag back off
- **WHEN** a user taps an already-selected ("✓ Бытовая химия") button again
- **THEN** the button reverts to "Бытовая химия" and the tag is removed from the pending selection

#### Scenario: User replies with free text to add a new tag
- **WHEN** a user has already toggled on "Бытовая химия" and then replies to the prompt with text "Скидка"
- **THEN** "Скидка" is added to the pending selection alongside "Бытовая химия" (both tags), and the prompt re-renders with "Скидка" also shown as selected if it's among the displayed suggestions, or confirmed via a short inline acknowledgement if not

#### Scenario: User replies with free text matching an existing tag's spelling
- **WHEN** a user replies with text that exactly matches an existing tag already used in the group (case-insensitive)
- **THEN** that existing tag is added to the pending selection (no duplicate tag "identity" is created)

#### Scenario: User taps "Готово" to confirm the selection
- **WHEN** a user has toggled on "Бытовая химия" and "Скидка" and taps "Готово"
- **THEN** the item(s) from that prompt are tagged with exactly `{"Бытовая химия", "Скидка"}`, replacing any previous tag set on those item(s), and a confirmation is sent (e.g. "✓ Теги «Бытовая химия», «Скидка» установлены")

#### Scenario: User taps "Готово" with no tags selected
- **WHEN** a user taps "Готово" without having toggled any suggestion or replied with free text
- **THEN** the item(s) end up with no tags, equivalent to tapping skip, and a neutral confirmation is sent

#### Scenario: User taps skip
- **WHEN** a user taps "Без категории" on the prompt
- **THEN** the item(s) from that prompt are left with no tags and the dialog is cleared without further confirmation text about tags

#### Scenario: User ignores the prompt entirely
- **WHEN** a user never replies to or taps a button on the tag prompt
- **THEN** the item(s) remain saved with whatever tags they already had (none, if newly added); the prompt has no expiry and can still be answered later unless discarded by a newer prompt (per "New tag-capture prompt discards a still-pending one")

---

### Requirement: Suggest frequently-used tags, ranked per group
The system SHALL rank tag suggestions by frequency of use across the whole group (not per individual user), sourced from `PurchaseHistoryTags` (not the live, ever-changing `ShoppingItems`/`ItemTags` state), so suggestions persist even after tagged items are bought and removed from the active list. The top 5 tags by frequency (ties broken by most recent use) are shown as toggleable suggestion buttons.

#### Scenario: Group has 5 or more distinct tags in its purchase history
- **WHEN** the tag prompt is shown and the group's purchase history has at least 5 distinct tags
- **THEN** the bot displays toggle buttons for the top 5 tags by frequency (ties broken by most recent use), plus "Без категории" and "Готово"

#### Scenario: Group has fewer than 5 distinct tags
- **WHEN** the tag prompt is shown and the group's purchase history has 2 distinct tags
- **THEN** the bot displays toggle buttons for both tags plus "Без категории" and "Готово"

#### Scenario: Group has no purchase history with tags yet
- **WHEN** the tag prompt is shown and the group has no purchase history tags
- **THEN** the bot displays only "Без категории" and "Готово"; no suggestion buttons appear

#### Scenario: Suggestions are shared across users in the group
- **WHEN** User A has previously tagged items "Химия" and "Авто", and User B (same group, different Telegram account) triggers a new tag prompt
- **THEN** User B sees "Химия" and "Авто" as suggestions, even though User B never used those tags themselves

#### Scenario: Tag button labels are truncated if too long
- **WHEN** a suggested tag exceeds 20 characters
- **THEN** the button label shows the first 20 characters followed by "…" (the ✓ prefix, if selected, does not count against the 20-character budget)

---

### Requirement: Tags flow from an item into its purchase history record
When an item that has one or more tags is later marked as bought, the system SHALL carry that full tag set through the price-capture dialog into the resulting `PurchaseHistory` record's `PurchaseHistoryTags`, so each tag contributes to future tag-suggestion ranking (per "Suggest frequently-used tags, ranked per group").

#### Scenario: Bought item's tags are saved to purchase history
- **WHEN** an item tagged `{"Химия", "Скидка"}` is marked bought and the price-capture dialog completes (with or without store/price/expiry provided)
- **THEN** the resulting `PurchaseHistory` row is linked to both the "Химия" and "Скидка" tags via `PurchaseHistoryTags`

#### Scenario: Bought item with no tags leaves purchase history untagged
- **WHEN** an item with no tags is marked bought
- **THEN** the resulting `PurchaseHistory` row has no `PurchaseHistoryTags` rows

---

### Requirement: Query distinct tags for the active list, and top tags from history
The system SHALL provide (a) a repository query listing all distinct tags currently used by *active* items in a group (for the `/list` filter row), ordered alphabetically (case-insensitive), and (b) a repository query listing the top-N distinct tags by frequency of use in the group's *purchase history* (for suggestion ranking), ties broken by most recent use.

#### Scenario: Distinct active-item tags are alphabetically ordered
- **WHEN** a group's active shopping list has items tagged "Химия", "Авто", and "Химия" again (on a different item)
- **THEN** the distinct-tags query (for `/list` filtering) returns `["Авто", "Химия"]`

#### Scenario: Top purchase-history tags ranked by frequency
- **WHEN** a group's purchase history tags "Химия" 5 times, "Авто" 2 times, and "Аптека" 1 time (across all `PurchaseHistoryTags` rows)
- **THEN** the top-3 tags query (for suggestions) returns `["Химия", "Авто", "Аптека"]` in that order

#### Scenario: No tags used yet
- **WHEN** a group has no active tagged items and no purchase history tags
- **THEN** both queries return an empty list

---

### Requirement: Existing single-category data is preserved as tags
The system SHALL migrate every pre-existing non-null `ShoppingItems.Category` and `PurchaseHistory.Category` value into the tag tables (`Tags`/`ItemTags`/`PurchaseHistoryTags`) exactly once, on first startup after this change deploys, without deleting or altering the original `Category` columns.

#### Scenario: Item with an existing category gains an equivalent tag
- **WHEN** the bot starts up for the first time after this change deploys, and a `ShoppingItems` row has `Category = "Бытовая химия"`
- **THEN** that item is linked via `ItemTags` to a "Бытовая химия" tag scoped to its group, and its `Category` column is left unchanged

#### Scenario: Backfill runs exactly once
- **WHEN** the bot restarts a second time after the backfill has already run
- **THEN** no duplicate `ItemTags`/`PurchaseHistoryTags` rows are created and no errors occur

#### Scenario: Two items in the same group with the same category value share one tag
- **WHEN** two different `ShoppingItems` rows in the same group both have `Category = "Авто"`
- **THEN** the backfill links both items to the same single "Авто" `Tags` row for that group, not two separate tag rows
