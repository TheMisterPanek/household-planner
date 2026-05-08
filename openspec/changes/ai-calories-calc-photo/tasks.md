## 1. Database and Domain Setup

- [ ] 1.1 Create `NutritionAnalysis` table in `DatabaseInitializer.StartAsync` with columns: Id, ChatId, PhotoFileId, AnalysisTimestamp, AiService, RawResponse, AppliedToListItemId, CreatedAt
- [ ] 1.2 Add migration logic for new columns on `ShoppingItems` table: `NutritionSource` (TEXT, "manual" | "photo-analysis"), `EstimatedCalories` (INTEGER, nullable), `AnalysisId` (INTEGER, FK, nullable)
- [ ] 1.3 Create `NutritionAnalysisResult`, `FoodItem`, and `PhotoUploadState` domain models
- [ ] 1.4 Create `NutritionAnalysisJsonContext` (source-gen `JsonSerializerContext`) for serializing/deserializing AI responses and nutrition data

## 2. AI Service Integration - Core Infrastructure

- [ ] 2.1 Create `IAiNutritionService` interface with method: `Task<NutritionAnalysisResult?> AnalyzeFoodPhotoAsync(byte[] imageBytes, CancellationToken ct)`
- [ ] 2.2 Create `OpenRouterNutritionService` implementation that constructs HTTP requests to OpenRouter API with base64-encoded image and helper prompt
- [ ] 2.3 Create `Gemma4LocalNutritionService` fallback implementation that sends requests to local Gemma-4 endpoint
- [ ] 2.4 Create `AiNutritionServiceFactory` that selects between OpenRouter and Gemma-4 based on availability; return default "unavailable" stub if neither is configured
- [ ] 2.5 Register `IAiNutritionService` in DI container (via factory) in `Program.cs`
- [ ] 2.6 Add environment variable support: `OPENROUTER_API_KEY`, `GEMMA4_LOCAL_ENDPOINT`
- [ ] 2.7 Add configuration logging to report which AI service is active at startup

## 3. Photo Download and Processing

- [ ] 3.1 Create `ITelegramPhotoDownloader` service interface with method: `Task<byte[]?> DownloadPhotoAsync(string fileId, CancellationToken ct)`
- [ ] 3.2 Implement `TelegramPhotoDownloader` using Telegram client to fetch file and download as bytes
- [ ] 3.3 Add timeout handling (30s max) for photo download with graceful error logging
- [ ] 3.4 Verify photo download is AOT-safe (no reflection in HTTP streaming)

## 4. Photo Upload Workflow and Handler

- [ ] 4.1 Create `PhotoUploadState` class for session state: ChatId, UserId, Timestamp, RetryCount
- [ ] 4.2 Create `/photo` command handler (`ICommandHandler`) that initiates photo upload session and sends localized prompt to user
- [ ] 4.3 Extend `PendingDialogService<T>` usage to manage `PhotoUploadState` sessions with 5-minute expiry
- [ ] 4.4 Create photo callback handler that matches callback prefix `photo_upload:<token>` pattern
- [ ] 4.5 Create message handler (`IDialogMessageHandler`) that intercepts photos during active upload session
- [ ] 4.6 Verify callback data fits 64-byte limit; token size is 8–12 characters

## 5. Nutrition Analysis and Response Parsing

- [ ] 5.1 Create `NutritionAnalyzer` service that orchestrates: download photo → send to AI → parse response → validate
- [ ] 5.2 Implement JSON parsing for AI response using source-gen context; handle malformed JSON gracefully
- [ ] 5.3 Add validation logic: reject negative/zero calories, flag implausibly high values (>10000), validate confidence field
- [ ] 5.4 Implement fallback from OpenRouter to Gemma-4 on 5xx errors or timeouts
- [ ] 5.5 Add error handling at handler level (no exceptions thrown; localized error messages sent to user)
- [ ] 5.6 Log all analysis attempts (success/failure) at Info level with AI service name

## 6. Database Persistence for Nutrition Analysis

- [ ] 6.1 Create `INutritionAnalysisRepository` interface with: `InsertAsync()`, `UpdateAppliedToListItemAsync()`
- [ ] 6.2 Implement `NutritionAnalysisRepository` using raw SQLite to insert and update `NutritionAnalysis` records
- [ ] 6.3 Store raw AI response as JSON TEXT using source-gen context
- [ ] 6.4 Handle database write failures gracefully (log Warning, do not suppress user response)

## 7. Shopping List Integration

- [ ] 7.1 Extend `ShoppingItem` domain model with `NutritionSource` (enum: Manual, PhotoAnalysis) and `EstimatedCalories` (nullable int)
- [ ] 7.2 Update `ShoppingListService.AddItemAsync()` to accept optional nutrition metadata
- [ ] 7.3 Create overload or builder for adding items from photo analysis: `AddItemFromPhotoAsync(chatId, name, portion, calories, analysisId)`
- [ ] 7.4 Extend `/list` message formatter to optionally display calorie estimates inline when available
- [ ] 7.5 Verify price-capture workflow works identically for photo-sourced and manual items

## 8. History Audit Integration

- [ ] 8.1 Add `BotActionType.PhotoAnalyzed` enum value to history action types
- [ ] 8.2 After successful photo analysis, call `IHistoryRepository.RecordAsync()` with analysis details (AI service, items, total calories)
- [ ] 8.3 Extend `ItemAdded` history payload to include `source` field ("manual" vs "photo-analysis") and optional `estimated_calories`
- [ ] 8.4 Extend `ItemBought` history payload to preserve original nutrition source and calories for audit trail
- [ ] 8.5 Wrap all history writes in try/catch; log failures at Warning level without suppressing user response

## 9. Localization

- [ ] 9.1 Add localization keys: `nutrition.photo.prompt`, `nutrition.photo.analyzing`, `nutrition.photo.success`, `nutrition.photo.error.download`, `nutrition.photo.error.analysis`, `nutrition.photo.error.unavailable`, `nutrition.photo.error.no_food`, `nutrition.photo.button.add`, `nutrition.photo.button.analyze_another`, `nutrition.photo.button.cancel`
- [ ] 9.2 Add Russian translations for all nutrition-related keys
- [ ] 9.3 Add confidence disclaimer key for medium confidence analyses
- [ ] 9.4 Verify no hardcoded user-facing strings in nutrition handlers/services

## 10. Unit Tests

- [ ] 10.1 Unit tests for `NutritionAnalyzer`: successful analysis, AI service fallback, malformed response handling, confidence validation
- [ ] 10.2 Unit tests for `NutritionAnalysisRepository`: insert, update applied-to-list-item operations
- [ ] 10.3 Unit tests for `OpenRouterNutritionService`: request construction, response parsing, error handling (5xx, timeout)
- [ ] 10.4 Unit tests for `Gemma4LocalNutritionService`: request construction and fallback behavior
- [ ] 10.5 Unit tests for `/photo` command handler: session creation, callback routing, error cases (private chat, session expiry)
- [ ] 10.6 Unit tests for photo message handler: correct interception of photos during active session
- [ ] 10.7 Unit tests for shopping list integration: adding items from photo, nutrition metadata preservation, history recording
- [ ] 10.8 Unit tests for history audit: `PhotoAnalyzed` entry creation, payload validation, failure handling

## 11. Integration Testing (BDD)

- [ ] 11.1 Create `nutrition-analysis.feature` SpecFlow scenarios covering: happy path (user sends photo → analysis → add to list), AI service fallback, error handling, confidence disclaimers
- [ ] 11.2 Create step definitions for photo analysis workflow
- [ ] 11.3 Test nutrition data flows end-to-end from `/photo` command through shopping list addition and history recording

## 12. Configuration and Deployment

- [ ] 12.1 Update `appsettings.json` with AI service configuration (API key, endpoints, timeouts)
- [ ] 12.2 Add documentation for deploying with/without local Gemma-4 (Docker setup for optional local inference)
- [ ] 12.3 Add environment variable validation at startup (warn if OpenRouter key is missing but Gemma-4 not available)
- [ ] 12.4 Update Dockerfile to support optional Gemma-4 container (or document separate setup)

## 13. Manual Testing

- [ ] 13.1 Test `/photo` command in group chat (initiate session, receive localized prompt)
- [ ] 13.2 Send a food photo and verify analysis completes; check displayed results format and confidence level
- [ ] 13.3 Test add-to-list from analysis results; verify item appears in shopping list with nutrition metadata
- [ ] 13.4 Test error scenarios: bad photo (non-food), AI service unavailable, network timeout
- [ ] 13.5 Verify fallback from OpenRouter to Gemma-4 works (disable OpenRouter key and retry)
- [ ] 13.6 Check history records PhotoAnalyzed entries with correct metadata
- [ ] 13.7 Verify price-capture dialog works normally for photo-sourced items
