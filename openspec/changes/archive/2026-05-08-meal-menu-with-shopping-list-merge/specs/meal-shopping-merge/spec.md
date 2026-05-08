## ADDED Requirements

### Requirement: Meal ingredients are added to the shopping list in bulk
Tapping [🛒 Add to list] (callback `meal:to_list:{mealId}`) on a meal view SHALL insert all meal ingredients into `ShoppingItems` for the group that have no name-collision with existing items. Conflict detection is case-insensitive on `Name`. All non-conflicting ingredients are inserted before any conflict prompt is shown.

#### Scenario: No conflicts — all items added immediately
- **WHEN** none of the meal's ingredients share a name (case-insensitive) with any current `ShoppingItem` for the group
- **THEN** all ingredients are inserted into `ShoppingItems` and the bot answers the callback with "✅ Added N items to shopping list"

#### Scenario: All items already on the list — all conflict
- **WHEN** every ingredient of the meal collides with an existing `ShoppingItem`
- **THEN** zero items are inserted immediately and the conflict resolution message is shown for all colliding ingredients

#### Scenario: Mixed: some items added, some conflict
- **WHEN** a meal has 5 ingredients and 2 share names with existing shopping items
- **THEN** the 3 non-conflicting ingredients are inserted immediately, and the conflict message is shown for only the 2 colliding items

---

### Requirement: Conflicts are shown in a single inline message for user resolution
When one or more conflicts exist, the system SHALL send a conflict-resolution message listing each conflicting ingredient. The message SHALL show the existing shopping-list quantity and the meal's needed quantity for each conflict. The inline keyboard SHALL have one row per unresolved conflict with two buttons: [✓ Skip] and [+ Add anyway].

#### Scenario: Conflict message format
- **WHEN** a conflict is detected for "Milk" (on list: 3l, needed: 2l)
- **THEN** the conflict message contains "Milk — on list: 3l | needed: 2l" and the keyboard row has `[✓ Skip]` (callback `meal:merge:skip:{sessionId}:0`) and `[+ Add anyway]` (callback `meal:merge:add:{sessionId}:0`)

#### Scenario: Multiple conflicts shown together
- **WHEN** two ingredients conflict
- **THEN** both are shown in the same message as separate rows in the inline keyboard, each with their own [✓ Skip] and [+ Add anyway] buttons

---

### Requirement: Each conflict is resolved independently via inline buttons
The system SHALL allow each conflict to be resolved independently via inline buttons. Tapping [✓ Skip] for a conflict marks that ingredient as skipped (not added). Tapping [+ Add anyway] inserts the ingredient into `ShoppingItems`. After each button tap the conflict message is edited to remove the resolved row from the keyboard.

#### Scenario: User skips a conflict
- **WHEN** a user taps [✓ Skip] for "Milk"
- **THEN** no `ShoppingItems` row is inserted for "Milk", the "Milk" row is removed from the conflict keyboard, and the message is edited in place

#### Scenario: User adds a conflicting item anyway
- **WHEN** a user taps [+ Add anyway] for "Eggs"
- **THEN** a `ShoppingItems` row is inserted for "Eggs" with the meal's quantity, the "Eggs" row is removed from the conflict keyboard, and the message is edited in place

#### Scenario: All conflicts resolved — final summary shown
- **WHEN** the last conflict row is resolved (skip or add)
- **THEN** the conflict message is edited to a final summary (e.g., "✅ Done! Added: Pasta, Bacon. Skipped: Milk.")

---

### Requirement: Expired or unknown merge sessions are handled gracefully
If a user taps a merge conflict button after the session has expired (> 15 minutes) or been cleaned up, the system SHALL answer the callback query with an error message and take no further action.

#### Scenario: Session not found
- **WHEN** a user taps [✓ Skip] or [+ Add anyway] and the session key is not in memory
- **THEN** the bot answers the callback query with "This request has expired — tap 🛒 again to retry" and does not attempt to modify the shopping list

---

### Requirement: Merge session state is stored in memory and cleaned up lazily
The `MealMergeService` SHALL hold active sessions in a `ConcurrentDictionary` keyed by an 8-character random alphanumeric session ID. On every session lookup, sessions older than 15 minutes SHALL be removed from the dictionary before returning the result.

#### Scenario: Session is retrievable within 15 minutes
- **WHEN** a merge session is created and a conflict button is tapped within 15 minutes
- **THEN** the session is found and the conflict is processed correctly

#### Scenario: Sessions older than 15 minutes are purged on next lookup
- **WHEN** a lookup occurs and there are sessions older than 15 minutes in the dictionary
- **THEN** those sessions are removed during the lookup, regardless of whether the requested session was expired
