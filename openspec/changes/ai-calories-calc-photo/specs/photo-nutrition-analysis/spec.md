## ADDED Requirements

### Requirement: User initiates photo-based food entry via /photo command
The system SHALL accept `/photo` in a group chat, request a food photo from the user, and guide them through the photo upload and analysis workflow.

#### Scenario: /photo command in group chat
- **WHEN** a group member sends `/photo`
- **THEN** the bot replies with a localized prompt asking for a food photo and starts a photo upload session

#### Scenario: /photo command in private chat
- **WHEN** a user sends `/photo` in a private chat
- **THEN** the bot replies with the standard localized "group chat only" message and does not start a session

#### Scenario: User sends a photo during upload session
- **WHEN** the user sends a photo message while a photo upload session is active
- **THEN** the bot acknowledges receipt and begins AI analysis of the photo

#### Scenario: User cancels photo upload session
- **WHEN** a user sends a text message other than a photo while a session is active (after 5 minutes of inactivity)
- **THEN** the session expires and the bot sends a localized "session expired" message

---

### Requirement: System analyzes food photo via AI vision model
The system SHALL download the food photo from Telegram, send it to an AI vision model for nutrition analysis, and extract food item details.

#### Scenario: Successful photo analysis
- **WHEN** a photo is received during an active upload session
- **THEN** the bot downloads the photo, sends it (as base64) to the AI service with a nutrition helper prompt, and receives a JSON response with food items and calorie estimates

#### Scenario: Photo download fails
- **WHEN** Telegram file download times out or fails
- **THEN** the bot replies with a localized error message ("Could not download photo, please try again") and logs the failure at Warning level without throwing

#### Scenario: AI service is unavailable (OpenRouter down)
- **WHEN** the OpenRouter API is unreachable or returns a 5xx error
- **THEN** the bot attempts fallback to local Gemma-4; if also unavailable, replies with a localized error ("AI service unavailable, please try again later") and logs at Warning level

#### Scenario: AI returns invalid/malformed response
- **WHEN** the AI service returns a response that does not match the expected JSON schema
- **THEN** the bot replies with a localized error ("Could not analyze this image, please try again") and logs the parsing error at Warning level

#### Scenario: Photo is not food
- **WHEN** the AI service returns low confidence (< 0.4) or a non-food classification
- **THEN** the bot replies with a localized message ("I couldn't identify food in this image. Please send a clear photo of a meal.") and allows the user to retry by sending another photo

---

### Requirement: Display analysis results to user
After successful analysis, the system SHALL present extracted food items, portions, and calorie estimates to the user with options to add items to the shopping list.

#### Scenario: Show analysis results with high confidence
- **WHEN** analysis completes with high confidence (≥ 0.7)
- **THEN** the bot displays a formatted message showing food items, portion sizes, estimated calories, and inline buttons: `[Add to List]` per item, `[Analyze Another]`, `[Cancel]`

#### Scenario: Show analysis results with medium confidence
- **WHEN** analysis completes with medium confidence (0.4–0.7)
- **THEN** the bot displays results with a confidence disclaimer and the same button options

#### Scenario: User adds item from analysis to shopping list
- **WHEN** user taps `[Add to List]` for a specific food item
- **THEN** the item (with portion and estimated calories in description) is added to the shopping list, a `NutritionAnalysis` record is saved with `AppliedToListItemId` set, and the bot sends a confirmation

#### Scenario: User analyzes another photo
- **WHEN** user taps `[Analyze Another]`
- **THEN** a new photo upload session is started from step 1

#### Scenario: User cancels analysis
- **WHEN** user taps `[Cancel]`
- **THEN** the session is cleared and the bot sends a confirmation

---

### Requirement: Store nutrition analysis results
The system SHALL persist photo metadata and AI analysis results in SQLite for audit, history, and future reference.

#### Scenario: Save successful analysis to database
- **WHEN** analysis completes and results are displayed to the user
- **THEN** a `NutritionAnalysis` record is inserted with: PhotoFileId, AiService used, RawResponse (JSON), Timestamp, and ChatId

#### Scenario: Link analysis to shopping list item when added
- **WHEN** user adds a food item from analysis to the shopping list
- **THEN** the `NutritionAnalysis.AppliedToListItemId` is updated to reference the new shopping list item

#### Scenario: Database write failure does not suppress user response
- **WHEN** `NutritionAnalysis` insert fails due to database error
- **THEN** the user-facing result is still displayed, the error is logged at Warning level, and the app does not throw

---

### Requirement: /photo command records PhotoAnalyzed history entry
The system SHALL record a `BotActionType.PhotoAnalyzed` history entry via `IHistoryRepository.RecordAsync` after a photo is successfully analyzed, including AI service used and food items detected.

#### Scenario: Photo analysis records history
- **WHEN** photo analysis completes successfully
- **THEN** a `PhotoAnalyzed` history entry is recorded with the AI service name, items detected, and total estimated calories

#### Scenario: Successful add-to-list from photo records history
- **WHEN** user adds a food item from photo analysis to the shopping list
- **THEN** an additional history entry is recorded linking the photo analysis to the item addition (or combined in payload)

#### Scenario: History write failure does not suppress confirmation
- **WHEN** `IHistoryRepository.RecordAsync` fails during photo analysis completion
- **THEN** the analysis results are still displayed to the user and the error is logged at Warning level
