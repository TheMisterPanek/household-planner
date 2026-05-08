## Context

The bot currently has no way to accept a Telegram photo as input. Users must type product names manually. This design covers adding a photo-message handler that decodes barcodes locally (ZXing.Net) and looks up product metadata from the Open Food Facts API, then offers the result as a pre-filled item name in the existing `item-search` flow.

No schema changes are needed — barcode lookup is a read-only, stateless action.

## Goals / Non-Goals

**Goals:**
- Decode EAN-8, EAN-13, UPC-A, UPC-E, Code-128 barcodes from a Telegram photo entirely on-device.
- Look up product name, brand, and quantity from Open Food Facts (free, no auth).
- Offer the resolved product name as a one-tap pre-fill into the item-search flow.
- Stay within the existing cross-cutting architecture (localization, history audit, DI, AOT).

**Non-Goals:**
- AI / vision models for barcode detection.
- Paid barcode database services.
- Storing barcode or product data locally (no new tables).
- QR codes (out of scope for v1; linear barcodes only).
- Group-chat barcode scanning (private chats only for v1, same restriction as most tracking flows).

## Decisions

### D1: Barcode library — ZXing.Net

**Choice**: `ZXing.Net` (NuGet: `ZXing.Net`).

**Why**: Well-maintained, supports all required formats (EAN-8/13, UPC-A/E, Code-128), pure managed C#, no native P/Invoke. Decodes from a `RGBLuminanceSource` built from raw JPEG/PNG bytes via `System.Drawing.Common` or `SkiaSharp`.

**AOT concern**: ZXing.Net uses generic type instantiation but not arbitrary reflection or `Activator.CreateInstance`. It is safe under .NET's trimming as long as the barcode reader types are not tree-shaken. Mitigation: wrap behind `IBarcodeDecoder` and annotate with `[DynamicallyAccessedMembers]` where needed; add a trim root in the `.csproj` if the linker removes ZXing types.

**Image decoding for luminance source**: Use `SkiaSharp` (`SkiaSharp.NativeAssets.Linux`) to convert JPEG/PNG bytes → `SKBitmap` → `RGBLuminanceSource`. SkiaSharp ships native binaries and is AOT-safe; `System.Drawing.Common` is not supported on Linux in AOT/trimmed builds.

**Alternative considered**: Dynamsoft Barcode Reader (paid, rejected), raw ZXing port (unmaintained), server-side scan API (network dependency, rejected to keep it offline).

### D2: Open Food Facts — thin HTTP client, no SDK

**Choice**: A hand-rolled `IProductLookupService` using `IHttpClientFactory` + `System.Text.Json` source-gen deserialization.

**Endpoint**: `GET https://world.openfoodfacts.org/api/v2/product/{barcode}.json`

**Response mapping** (source-gen DTO):
```
OpenFoodFactsResponse { int status; ProductDto product }
ProductDto { string product_name; string brands; string quantity }
```

`status == 1` → found; `status == 0` → not found.

**Why no SDK**: No official .NET SDK exists. A thin client is ~30 lines, fully AOT-safe, and avoids a large dependency.

**Timeout**: 5 s (configurable via env var `OFF_TIMEOUT_SECONDS`). On timeout or HTTP error: log Warning, fall back to "product not found" reply.

**Rate limiting**: Open Food Facts asks for a descriptive `User-Agent`. Set `User-Agent: ProductTrackerBot/1.0 (contact: <MAINTAINER_EMAIL>)` via `HttpClient` default headers.

### D3: Photo handler — dedicated `IPhotoMessageHandler`

The existing dispatcher handles `/commands` and callback buttons. Photos arrive as `Message.Photo` updates (not text). Introduce a new routing interface:

```
interface IPhotoMessageHandler
    Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
```

The main `UpdateHandler` (or its equivalent dispatcher) checks `update.Message.Photo != null` and routes to the registered `IPhotoMessageHandler`. Only one implementation exists (`BarcodePhotoHandler`).

**Photo download**: Telegram sends multiple resolutions; take the largest (`Photo.Last()` — Telegram orders ascending by size). Download via `ITelegramBotClient.GetFileAsync` + `HttpClient` to a `MemoryStream`. Enforce a 10 MB cap to avoid memory abuse.

**Flow**:
```
User sends photo
  → UpdateHandler routes to BarcodePhotoHandler
    → Download largest photo (≤10 MB)
    → IBarcodeDecoder.DecodeAsync(bytes)
    → [no barcode] reply localized "barcode_not_found_in_photo", return
    → [barcode found] IProductLookupService.LookupAsync(barcode)
      → [product found] reply "{product_name} ({brand}, {quantity})" + inline button "Add as item"
      → [not found] reply localized "product_not_found_barcode" with raw barcode value
    → IHistoryRepository.RecordAsync("barcode_scan", chatId, barcode/product)
```

### D4: "Add as item" callback — session token pattern

The inline button payload must fit 64 bytes. A full product name can exceed that limit.

**Choice**: Store `{ barcode, productName }` in an in-memory session (`Dictionary<string, BarcodeSession>`) keyed by a 6-character random token. Callback data: `barcode_add:{token}` (13 bytes max). Session TTL: 10 minutes (no persistence needed — user will act immediately or not at all).

A new `ICallbackHandler` with `CallbackPrefix = "barcode_add"` retrieves the session, pre-fills the item name, and triggers the item-search dialog entry point.

### D5: item-search integration — entry-point reuse

`item-search` already has a dialog entry point that accepts a pre-filled term. The `barcode_add` callback sets the term and delegates to that entry point — no changes to item-search's internal state machine.

The `item-search` spec notes this as an allowed integration point ("Modified Capabilities" in proposal).

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| ZXing.Net trimmed away in AOT build | Add trim roots in `.csproj`; integration test in Docker image |
| SkiaSharp native lib missing in Docker image | Add `SkiaSharp.NativeAssets.Linux` package; verify in Docker build |
| Open Food Facts API unavailable or slow | 5 s timeout, graceful fallback reply, no retry (user can resend photo) |
| Photo > 10 MB | Reject with localized message before download attempt (check Telegram file_size field) |
| Barcode unreadable (blur, angle) | Reply with localized "no barcode found"; user can try a clearer photo |
| Session store unbounded growth | Fixed 10-min TTL + background cleanup on each access |
| Product name with special characters in callback session | Stored in-memory, not in callback data — no escaping issue |

## Migration Plan

1. Add NuGet packages (`ZXing.Net`, `SkiaSharp`, `SkiaSharp.NativeAssets.Linux`).
2. Implement `IBarcodeDecoder`, `BarcodeDecoder` (ZXing.Net + SkiaSharp).
3. Implement `IProductLookupService`, `OpenFoodFactsProductLookupService` (HttpClient + source-gen DTOs).
4. Implement `BarcodePhotoHandler` + `BarcodeSessionService`.
5. Implement `BarcodeAddCallbackHandler`.
6. Wire photo routing in `UpdateHandler`.
7. Register all new services in DI.
8. Add localization keys.
9. Build Docker image, verify SkiaSharp native libs load.

**Rollback**: Unregister `IPhotoMessageHandler` in DI. No DB rollback needed.

## Open Questions

- Should group chats be supported in v1? (Current decision: no — simpler, avoids group-context complexity.)
- Should the raw barcode value be offered as an item name if Open Food Facts returns no result? (Proposed: yes — show barcode as fallback item name with a note.)
