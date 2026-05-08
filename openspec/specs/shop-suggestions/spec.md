# shop-suggestions Specification

## Purpose
TBD - created by archiving change suggest-popular-shops. Update Purpose after archive.
## Requirements
### Requirement: Suggest popular shops when capturing store name
The system SHALL display the 5 most frequently visited shops for the user in the current group as inline buttons when the price-capture dialog reaches step 1 ("📍 Where did you buy {item}?"). Shop suggestions are derived from the user's prior purchase history and are ranked by frequency (most frequent first).

#### Scenario: User has prior purchases with shop names
- **WHEN** price-capture dialog enters step 1 and the user has at least 1 prior purchase with a non-null `StoreName` in the group
- **THEN** the bot displays inline buttons for the top 5 shops by frequency, plus a [Skip] button, below the "Where did you buy?" prompt

#### Scenario: User has fewer than 5 prior shop names
- **WHEN** price-capture dialog enters step 1 and the user has fewer than 5 distinct shop names in their history
- **THEN** the bot displays buttons for all distinct shops found (e.g., 3 shops + [Skip] button)

#### Scenario: User has no prior purchases with shop names
- **WHEN** price-capture dialog enters step 1 and the user has no prior purchases with `StoreName` set (all are null)
- **THEN** the bot displays only the [Skip] button and waits for free-text input; no suggestions appear

#### Scenario: User taps a suggested shop button
- **WHEN** user taps one of the shop suggestion buttons during step 1
- **THEN** `StoreName` is set to the tapped button's shop name, step advances to 2, and the bot asks "💰 Price for {item}?" with a [Skip] button

#### Scenario: User types custom shop name instead of using a suggestion
- **WHEN** user replies with a text message during step 1 instead of tapping a button
- **THEN** the typed text is set as `StoreName`, step advances to 2, and the bot asks for price; the custom text overrides any suggested shops

#### Scenario: Shop names are truncated if too long
- **WHEN** a shop name in the user's history exceeds 30 characters
- **THEN** the button label shows the first 30 characters followed by "…" (ellipsis)

---

### Requirement: Top shops are queried by frequency and availability
The system SHALL query the user's top shops from the `PurchaseHistory` table, filtering by group and user, ordered by frequency (count of purchases) descending, with ties broken by most-recent `PurchasedAt` descending.

#### Scenario: Shops ranked by purchase frequency
- **WHEN** the user has purchased from "Carrefour" 5 times, "Stokrotka" 3 times, and "Decathlon" 1 time
- **THEN** the top shops list is ["Carrefour", "Stokrotka", "Decathlon"] in that order

#### Scenario: Null shop names are excluded from suggestions
- **WHEN** querying top shops for a user whose history includes purchases with `StoreName = null`
- **THEN** null entries are filtered out and do not appear in the suggestions list

#### Scenario: Top shops are queried per user per group
- **WHEN** two users (A and B) in the same group request top shops
- **THEN** each user sees only their own most-frequent shops; User A's list does not influence User B's suggestions

