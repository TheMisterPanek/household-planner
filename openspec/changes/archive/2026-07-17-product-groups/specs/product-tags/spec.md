## ADDED Requirements

### Requirement: Prompt for a category after an item is added
The system SHALL start a category-capture follow-up immediately after an item (or, for bulk add, a batch of items) is successfully persisted via `/buy`, sending a message asking which category to file it under, with inline buttons for the group's suggested categories (per "Suggest frequently-used categories") plus a "Без категории" skip button. This applies to inline single-item add, the `/buy` dialog (both the two-step flow and one-line re-entry), the skip-quantity path, and bulk comma-separated add.

#### Scenario: Category prompt follows a single-item add
- **WHEN** an item is successfully added via any single-item `/buy` path (inline, dialog, or skip-quantity) and the "Иван добавил(а) X" confirmation is sent
- **THEN** the bot sends a separate follow-up message "В какую группу добавить X?" with category suggestion buttons and a "Без категории" button

#### Scenario: Category prompt follows a bulk add, asked once for the whole batch
- **WHEN** multiple items are added via comma-separated bulk `/buy` and the bulk confirmation is sent
- **THEN** the bot sends one follow-up message asking which category to file all of the newly-added items under, not one prompt per item

#### Scenario: Category prompt follows an item edit
- **WHEN** a user saves an edit to an item's name/quantity via the one-line edit flow
- **THEN** the bot sends the same category follow-up prompt for that single item after the edit is saved, allowing the category to be added, changed, or left as-is

#### Scenario: New category-capture prompt discards a still-pending one
- **WHEN** a user has an unanswered category prompt pending and adds another item (or edits another item) before answering it
- **THEN** the new prompt replaces the old pending one; the earlier item is left with no category assigned unless it already had one

---

### Requirement: Tap a suggested category or reply with free text to create a new one
The system SHALL let a user resolve the category-capture prompt either by tapping one of the suggested category buttons, or by replying with any text — the reply text becomes the category, whether or not it matches an existing category (i.e., a new category is created simply by typing it). Tapping the skip button leaves the category unset.

#### Scenario: User taps a suggested category button
- **WHEN** a user taps a suggested category button on the prompt
- **THEN** the tapped category is applied to the item(s) from that prompt, and a confirmation is sent (e.g. "✓ Категория «Бытовая химия» установлена")

#### Scenario: User replies with free text to create a new category
- **WHEN** a user replies to the category prompt with text that does not match any suggested button (e.g. "Для стирки")
- **THEN** the typed text is applied as the category for the item(s) from that prompt, and a confirmation is sent

#### Scenario: User replies with free text matching an existing category's spelling
- **WHEN** a user replies with text that exactly matches an existing category already used in the group
- **THEN** the item(s) are tagged with that same category (no duplicate category "identity" is created — categories are plain text, not a managed entity)

#### Scenario: User taps skip
- **WHEN** a user taps "Без категории" on the prompt
- **THEN** the item(s) from that prompt are left with `Category = null` and the dialog is cleared without further confirmation text about a category

#### Scenario: User ignores the prompt entirely
- **WHEN** a user never replies to or taps a button on the category prompt
- **THEN** the item(s) remain saved with no category (they were already persisted before the prompt was shown); the prompt has no expiry and can still be answered later unless discarded by a newer prompt (per "New category-capture prompt discards a still-pending one")

---

### Requirement: Suggest frequently-used categories, ranked per group
The system SHALL rank category suggestions by frequency of use across the whole group (not per individual user), sourced from `PurchaseHistory.Category` values (not the live, ever-changing `ShoppingItems` list), so suggestions persist even after tagged items are bought and removed from the active list. The top 5 categories by frequency (ties broken by most recent use) are shown as suggestion buttons.

#### Scenario: Group has 5 or more distinct categories in its purchase history
- **WHEN** the category prompt is shown and the group's purchase history has at least 5 distinct categories
- **THEN** the bot displays buttons for the top 5 categories by frequency (ties broken by most recent use), plus the skip button

#### Scenario: Group has fewer than 5 distinct categories
- **WHEN** the category prompt is shown and the group's purchase history has 2 distinct categories
- **THEN** the bot displays buttons for both categories plus the skip button

#### Scenario: Group has no purchase history with categories yet
- **WHEN** the category prompt is shown and the group has no purchase history with a non-null `Category`
- **THEN** the bot displays only the skip button; no suggestion buttons appear

#### Scenario: Suggestions are shared across users in the group
- **WHEN** User A has previously tagged items "Химия" and "Авто", and User B (same group, different Telegram account) triggers a new category prompt
- **THEN** User B sees "Химия" and "Авто" as suggestions, even though User B never used those categories themselves

#### Scenario: Category button labels are truncated if too long
- **WHEN** a suggested category exceeds 20 characters
- **THEN** the button label shows the first 20 characters followed by "…"

---

### Requirement: Category flows from an item into its purchase history record
When an item that has a `Category` set is later marked as bought, the system SHALL carry that category value through the price-capture dialog into the resulting `PurchaseHistory` record, so it contributes to future category-suggestion ranking (per "Suggest frequently-used categories, ranked per group").

#### Scenario: Bought item's category is saved to purchase history
- **WHEN** an item with `Category = "Химия"` is marked bought and the price-capture dialog completes (with or without store/price/expiry provided)
- **THEN** the resulting `PurchaseHistory` row has `Category = "Химия"`

#### Scenario: Bought item with no category leaves purchase history category null
- **WHEN** an item with `Category = null` is marked bought
- **THEN** the resulting `PurchaseHistory` row has `Category = null`

---

### Requirement: Query distinct categories for the active list, and top categories from history
The system SHALL provide (a) a repository query listing all distinct non-null `Category` values currently used by *active* items in a group (for the `/list` filter row), ordered alphabetically (case-insensitive), and (b) a repository query listing the top-N distinct categories by frequency of use in the group's *purchase history* (for suggestion ranking), ties broken by most recent use.

#### Scenario: Distinct active-item categories are alphabetically ordered
- **WHEN** a group's active shopping list has items tagged "Химия", "Авто", and "Химия" again
- **THEN** the distinct-categories query (for `/list` filtering) returns `["Авто", "Химия"]`

#### Scenario: Top purchase-history categories ranked by frequency
- **WHEN** a group's purchase history has "Химия" 5 times, "Авто" 2 times, and "Аптека" 1 time
- **THEN** the top-3 categories query (for suggestions) returns `["Химия", "Авто", "Аптека"]` in that order

#### Scenario: No categories used yet
- **WHEN** a group has no active items and no purchase history with a non-null `Category`
- **THEN** both queries return an empty list
