## ADDED Requirements

### Requirement: Login page displays a login code on load
The system SHALL display a 6-character uppercase alphanumeric login code inside a `<span>` with class `font-mono` when `/login` is first rendered.

#### Scenario: Code is visible after page load
- **WHEN** a user navigates to `/login`
- **THEN** a non-empty code matching `[A-Z0-9]{6}` is visible on the page

### Requirement: Login page shows a countdown timer
The system SHALL display the text "Code expires in N s" (where N ≤ 300) while the code has not yet expired.

#### Scenario: Countdown text is visible on fresh load
- **WHEN** a user navigates to `/login`
- **THEN** the page shows text matching "Code expires in \d+ s"
- **AND** the "Get new code" button is NOT visible

#### Scenario: Countdown decrements over time
- **WHEN** a user navigates to `/login` and waits 2 seconds
- **THEN** the countdown value shown is less than the initial value

### Requirement: Login page shows expiry state and refresh button
When the code expires the system SHALL hide the countdown text and display a "Get new code" button.

#### Scenario: Expiry state shows refresh button
- **WHEN** the login code has expired (countdown reaches 0)
- **THEN** a "Get new code" button is visible
- **AND** the countdown text is no longer visible

### Requirement: Refresh generates a new code
Clicking "Get new code" SHALL call `GenerateCode` again, display a new code, and restart the countdown.

#### Scenario: New code is generated on refresh
- **WHEN** the code has expired and the user clicks "Get new code"
- **THEN** a new non-empty code is displayed
- **AND** the countdown text is visible again
- **AND** the "Get new code" button is no longer visible

### Requirement: Login page title is correct
The page SHALL have the title "Sign in — Household Planner".

#### Scenario: Page title matches expected value
- **WHEN** a user navigates to `/login`
- **THEN** the document title is "Sign in — Household Planner"

### Requirement: Bot username is shown in the instructions
The page SHALL display the bot username (from configuration) in the instruction text so the user knows which bot to message.

#### Scenario: Bot username appears in instructions
- **WHEN** `BOT_USERNAME` is configured as `@TestBot`
- **AND** a user navigates to `/login`
- **THEN** the text `@TestBot` is visible on the page
