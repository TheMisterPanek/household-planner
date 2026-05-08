## Context

The bot currently supports manual food entry via `/buy` and shopping list management. Users must search and specify food items and quantities manually. To streamline this, we integrate an AI vision model that analyzes food photos and extracts nutrition data automatically. This requires:

1. **Photo upload flow**: Telegram callback to request and receive photos
2. **AI service integration**: HTTP client to OpenRouter (primary) with local fallback to Gemma-4
3. **Nutrition data extraction**: Parse AI responses into structured food items with calorie estimates
4. **Data persistence**: Store analysis results linked to shopping list items

## Goals / Non-Goals

**Goals:**
- Enable food logging via photo with minimal user friction (one photo → nutrition estimate)
- Support both cloud-based AI (OpenRouter) and local inference (Gemma-4) for resilience
- Parse AI vision responses into actionable nutrition data (food type, portion, calories)
- Maintain AOT compatibility; no reflection-based serialization
- Integrate seamlessly with existing shopping list and history audit patterns

**Non-Goals:**
- Real-time meal tracking or nutrition dashboard (log only)
- Support for barcode scanning or database lookups (AI-only approach)
- Multi-step photo correction workflows (user feedback is out of scope for v1)
- Batch photo processing (single photo at a time)

## Decisions

### 1. AI Service Selection: OpenRouter Primary, Local Gemma-4 Fallback

**Decision**: Use OpenRouter API as the primary AI vision service. If unavailable or misconfigured, fall back to locally running Gemma-4.

**Rationale**:
- OpenRouter provides a unified API for multiple vision models (Claude Vision, GPT-4V, etc.) with pay-as-you-go pricing
- Local Gemma-4 runs on modest hardware, removes dependency on external APIs for sensitive food data
- Single service abstraction (`IAiNutritionService`) enables both implementations without duplication

**Alternatives Considered**:
- Direct OpenAI API: More expensive, single model, no fallback built-in
- Direct Anthropic API: No vision model officially supported for nutrition analysis at proposal time
- Replicate (now Roboflow): Less mature, higher latency than OpenRouter

### 2. Photo Download and Storage

**Decision**: Download photos from Telegram inline, process immediately, discard after analysis. Do not store photo files on disk.

**Rationale**:
- Avoids disk I/O and cleanup complexity
- Complies with privacy (food photos are personal data)
- Reduces storage footprint for the Docker image
- Simpler deployment: no volume mounts for photos needed

**Process**:
1. User sends photo via `/photo` → callback handler receives file_id
2. Download via Telegram's `getFile` → returns file path, then `downloadFile` to stream
3. Convert to bytes, send to AI service as base64-encoded image
4. Discard bytes after analysis

### 3. Callback Data Design: Session Token for Photo Upload Flow

**Decision**: Use in-memory session service with short random token stored in callback data; session holds pending photo request state.

**Rationale**:
- Photo upload request (`/photo` → user sends photo) spans multiple steps
- Callback data has 64-byte limit; full state (chat_id, user_id, timestamp) exceeds this
- Use token (8–12 chars) to key a short-lived session in `PendingDialogService<PhotoUploadState>`
- Session expires after 5 minutes of inactivity

**Callback Prefix**: `photo_upload:<token>` (16 bytes + token)

### 4. AI Response Parsing and Extraction

**Decision**: Use a helper prompt to guide the AI model toward structured JSON output. Parse response with `System.Text.Json` source-gen deserialization.

**Rationale**:
- Vision models are unpredictable in free-form output; a structured prompt greatly improves consistency
- Example helper prompt: "Analyze this food photo. Respond with JSON: {\"items\": [{\"name\": \"...\", \"portion\": \"...\", \"calories\": number}]}"
- Source-gen serialization is AOT-safe and requires no reflection

**Response Schema** (define in `JsonSerializerContext`):
```csharp
public record NutritionAnalysisResult(
    List<FoodItem> Items,
    string Confidence, // "high" | "medium" | "low"
    string Notes
);

public record FoodItem(
    string Name,
    string Portion,
    int EstimatedCalories
);
```

### 5. Persistence: Analysis Results in SQLite

**Decision**: Store nutrition analysis metadata in a new `NutritionAnalysis` table; full AI response as JSON TEXT.

**Rationale**:
- Supports querying food history by date, type, or calorie range
- JSON storage preserves raw AI output for audit/debugging
- Follows existing pattern: JSON payloads stored as TEXT via source-gen context

**Schema**:
```sql
CREATE TABLE IF NOT EXISTS NutritionAnalysis (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChatId TEXT NOT NULL,
    PhotoFileId TEXT NOT NULL,
    AnalysisTimestamp TEXT NOT NULL, -- ISO-8601 UTC
    AiService TEXT NOT NULL, -- "openrouter" | "gemma4-local"
    RawResponse TEXT NOT NULL, -- JSON serialized NutritionAnalysisResult
    AppliedToListItemId INTEGER, -- FK if user links to shopping list item
    CreatedAt TEXT NOT NULL -- ISO-8601 UTC
);

CREATE INDEX idx_nutrition_chat_date ON NutritionAnalysis(ChatId, AnalysisTimestamp DESC);
```

### 6. Error Handling and Fallback

**Decision**: On AI service failure, reply with a localized error message; do not throw. Log failure at Warning level. Do not automatically retry (user can send photo again).

**Rationale**:
- Aligns with bot's error-handling pattern (no exceptions at handler level)
- User can retry by sending another photo
- Clear feedback prevents silent failures

**Handling**:
- Network timeout (> 30s): "AI service unavailable, please try again later"
- Invalid photo (e.g., not food): "Could not analyze this image. Please send a clear photo of food"
- Quota exceeded (OpenRouter): Fall back to Gemma-4; if also unavailable, inform user
- Malformed AI response: Log error, ask user to retry or enter manually

### 7. Localization and User Feedback

**Decision**: All prompts, buttons, error messages use `ILocalizer.Get(chatId, key)`. No hardcoded strings.

**Keys to define**:
- `nutrition.photo.prompt` – ask for photo
- `nutrition.photo.analyzing` – processing indicator
- `nutrition.photo.success` – analysis complete, show results
- `nutrition.photo.error.*` – error variants

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| **AI service downtime** (OpenRouter offline) | Fallback to local Gemma-4; if both unavailable, gracefully degrade to manual entry prompt |
| **Inaccurate calorie estimates** | Display confidence level from AI response; user can adjust before adding to list |
| **Photo privacy / data retention** | Never store photos on disk; discard immediately after analysis. Log only metadata. |
| **Session token collisions** | Use cryptographically secure random token (16+ chars) with low collision probability |
| **AOT compatibility** | Avoid reflection; use source-gen `JsonSerializerContext` for all JSON; no dynamic LINQ or EF Core |
| **Telegram API rate limits** | Cache photo file_ids; do not re-download same photo twice in quick succession |
| **Callback data size** | Token-based session design keeps callback ≤64 bytes |

## Migration Plan

**Deploy**:
1. Create `NutritionAnalysis` table via `DatabaseInitializer` (`CREATE TABLE IF NOT EXISTS`)
2. Register `IAiNutritionService` and `PhotoUploadHandler` in DI container
3. Register new localization keys
4. Deploy and monitor AI service latency and error rates

**Rollback**:
1. Remove `/photo` command and callback handler
2. Archive `photo-nutrition-analysis` capability spec
3. Keep `NutritionAnalysis` table (no data loss); mark as legacy

## Open Questions

1. **Gemma-4 setup**: Will local Gemma-4 be deployed in the same Docker container or as a separate service? (Affects configuration and fallback logic)
2. **OpenRouter API key**: Where will the key be stored? (Env var, config file, or secrets manager?)
3. **Calorie estimate accuracy**: Should we validate AI estimates against a nutrition database, or trust the model? (Affects design of post-processing)
4. **Multi-language prompts**: Should helper prompts be localized based on user locale, or always English for AI consistency?
