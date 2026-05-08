## ADDED Requirements

### Requirement: Configure and initialize AI service
The system SHALL support configuration of an AI vision service with fallback logic. Primary service is OpenRouter; fallback is local Gemma-4.

#### Scenario: OpenRouter is configured and available
- **WHEN** application starts with `OPENROUTER_API_KEY` environment variable set
- **THEN** `IAiNutritionService` is initialized with OpenRouter client and uses it for all analysis requests

#### Scenario: OpenRouter is unavailable or misconfigured
- **WHEN** OpenRouter API key is missing, invalid, or the service is unreachable
- **THEN** the system falls back to local Gemma-4 if available; if also unavailable, analysis requests fail gracefully with user-visible error

#### Scenario: Local Gemma-4 is configured
- **WHEN** `GEMMA4_LOCAL_ENDPOINT` environment variable points to a running Gemma-4 inference server
- **THEN** Gemma-4 is used as the fallback AI service

#### Scenario: No AI service is available
- **WHEN** both OpenRouter and Gemma-4 are unavailable
- **THEN** `/photo` command is disabled; attempting to use it replies with "AI service unavailable, contact administrator"

---

### Requirement: Send photo to AI service for nutrition analysis
The system SHALL format the photo as base64, construct a nutrition helper prompt, and send it to the active AI service.

#### Scenario: Send request to OpenRouter
- **WHEN** a photo is ready for analysis and OpenRouter is active
- **THEN** the bot constructs a request with: base64 image, helper prompt asking for JSON with {items: [{name, portion, calories}]}, and sends it via OpenRouter API

#### Scenario: Send request to local Gemma-4
- **WHEN** a photo is ready for analysis and OpenRouter is unavailable but Gemma-4 is active
- **THEN** the bot sends the request to the local Gemma-4 endpoint with the same prompt structure

#### Scenario: Helper prompt includes structured JSON format
- **WHEN** constructing the AI request
- **THEN** the prompt explicitly instructs the model to respond in JSON format: {items: [{name: "food name", portion: "portion size", calories: number}], confidence: "high|medium|low", notes: "optional notes"}

#### Scenario: Photo is converted to base64 for transmission
- **WHEN** a photo is downloaded from Telegram
- **THEN** the binary photo data is converted to base64 and embedded in the API request (not transmitted as a separate file)

#### Scenario: Request timeout
- **WHEN** AI service does not respond within 30 seconds
- **THEN** the request is cancelled, the failure is logged at Warning level, and fallback logic is triggered

---

### Requirement: Parse AI response and extract nutrition data
The system SHALL deserialize the AI response into a structured format and validate the extracted data.

#### Scenario: Parse successful JSON response
- **WHEN** AI service returns a response matching the expected schema
- **THEN** the response is deserialized into a `NutritionAnalysisResult` object containing items, confidence, and notes

#### Scenario: Missing required fields in AI response
- **WHEN** AI response is valid JSON but missing required fields (e.g., no `items` array)
- **THEN** parsing fails gracefully, error is logged at Warning level, and the user receives a localized error message

#### Scenario: Invalid confidence value
- **WHEN** AI response includes a confidence value outside the expected set (not "high", "medium", "low")
- **THEN** the confidence defaults to "low" and processing continues with a warning log

#### Scenario: Negative or zero calorie estimate
- **WHEN** AI response includes a non-positive calorie value for a food item
- **THEN** the value is rejected and the item is flagged with a note "Could not estimate calories"

#### Scenario: Extremely high calorie estimate (> 10000)
- **WHEN** AI response includes an implausible calorie value
- **THEN** the item is accepted but flagged with a note "Calorie estimate may be inaccurate — please verify"

---

### Requirement: Handle AI service errors and retries
The system SHALL handle transient and permanent AI service failures without throwing exceptions at the handler level.

#### Scenario: OpenRouter quota exceeded
- **WHEN** OpenRouter API returns a 429 (rate limit) error
- **THEN** fallback to Gemma-4 is triggered; if Gemma-4 is also unavailable, user receives "Service temporarily unavailable" message and error is logged at Warning level

#### Scenario: OpenRouter API error (5xx)
- **WHEN** OpenRouter API returns a 5xx error
- **THEN** fallback to Gemma-4 is triggered immediately

#### Scenario: Network timeout on AI request
- **WHEN** HTTP request to AI service times out after 30 seconds
- **THEN** the request is aborted, error is logged at Warning level, and user receives "Service unavailable" message

#### Scenario: Malformed base64 image
- **WHEN** photo-to-base64 conversion fails (file corruption)
- **THEN** error is logged at Warning level and user receives "Could not process photo, please try again"

#### Scenario: No automatic retry on failure
- **WHEN** analysis fails
- **THEN** user must manually send another photo to retry; no background retry queue is implemented

---

### Requirement: Maintain separation between primary and fallback services
The system SHALL clearly distinguish between OpenRouter (primary) and Gemma-4 (fallback) in logs and records.

#### Scenario: Log AI service used for each analysis
- **WHEN** analysis completes successfully
- **THEN** the `AiService` field in the `NutritionAnalysis` database record is set to "openrouter" or "gemma4-local"

#### Scenario: Fallback is transparent to user
- **WHEN** OpenRouter is used for analysis
- **THEN** user does not see which service processed the request; fallback to Gemma-4 is automatic and silent

#### Scenario: Monitor fallback frequency
- **WHEN** analysis requests are processed
- **THEN** every fallback from OpenRouter to Gemma-4 is logged at Info level for monitoring and alerting
