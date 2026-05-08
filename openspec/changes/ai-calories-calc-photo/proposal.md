## Why

Users need a convenient way to log food items by taking photos rather than manually searching and entering details. By integrating an AI vision model, we can automatically detect food items, estimate portions, and calculate calories from a single photo, reducing friction in food logging.

## What Changes

- Add photo upload capability to the Telegram bot (handled via callback with file download)
- Integrate with an external AI service (OpenRouter, or fallback to local Gemma-4) to analyze food images
- Parse AI responses to extract food item, portion size, and calorie estimates
- Store nutrition analysis results linked to shopping list items
- Add `/photo` command to trigger photo-based calorie logging workflow
- Create helper prompts to guide the AI vision model toward accurate nutrition analysis

## Capabilities

### New Capabilities
- `photo-nutrition-analysis`: Analyze food photos via AI vision model to extract food type, portion size, and estimated calories
- `ai-service-integration`: Manage integration with external AI services (OpenRouter) with fallback support for local models (Gemma-4)

### Modified Capabilities
- `shopping-list`: Extend to support nutrition data from photo analysis; log source (manual vs photo-based)

## Impact

- **Code**: New `NutritionAnalysisService`, `PhotoUploadHandler`, `AiServiceClient`
- **APIs**: New Telegram callback flow for photo uploads; new internal service boundary for AI integration
- **Dependencies**: HTTP client for OpenRouter API; optional local AI runtime for Gemma-4 fallback
- **Systems**: Storage for photo metadata and analysis results in SQLite; integration with Telegram file download APIs
- **Compatibility**: Photo download and file handling must remain AOT-safe; no reflection in serialization of AI responses

## Rollback Plan

If AI integration becomes unreliable or costs escalate, revert to manual food entry. Archive photo-nutrition-analysis capability and remove `/photo` command. Migration: keep existing nutrition data, mark source as "legacy-photo" for audit.

## Affected Teams

Single-user bot; impacts personal food tracking workflow.

## Cross-Cutting Dependencies

- **Persistence**: Nutrition analysis results stored as JSON in SQLite (via `System.Text.Json` source-generated context)
- **Localization**: User prompts for photo upload, error messages, and AI response parsing feedback all use `ILocalizer`
- **History Audit**: Every successful photo analysis logged via `IHistoryRepository.RecordAsync`
- **Error Handling**: Photo download failures and AI service timeouts handled gracefully with user-facing localized messages, no exceptions thrown at handler level
