## 1. NuGet Dependencies

- [ ] 1.1 Add `ZXing.Net` NuGet package to the project
- [ ] 1.2 Add `SkiaSharp` and `SkiaSharp.NativeAssets.Linux` NuGet packages
- [ ] 1.3 Verify trim-root or `[DynamicallyAccessedMembers]` annotations are not needed for ZXing.Net in a publish-trimmed build (test with `dotnet publish -r linux-x64 --self-contained`)

## 2. Barcode Decoding Service

- [ ] 2.1 Define `IBarcodeDecoder` interface with `Task<string?> DecodeAsync(byte[] imageBytes, CancellationToken ct)` returning the decoded barcode string or null
- [ ] 2.2 Implement `BarcodeDecoder`: convert image bytes to `SKBitmap` via SkiaSharp, build `RGBLuminanceSource`, run `BarcodeReader` (ZXing.Net) for EAN-8, EAN-13, UPC-A, UPC-E, Code-128
- [ ] 2.3 Register `IBarcodeDecoder` → `BarcodeDecoder` as singleton in DI

## 3. Open Food Facts Product Lookup

- [ ] 3.1 Define `IProductLookupService` interface with `Task<ProductInfo?> LookupAsync(string barcode, CancellationToken ct)`
- [ ] 3.2 Create `ProductInfo` record: `string ProductName`, `string? Brand`, `string? Quantity`
- [ ] 3.3 Create `OpenFoodFactsResponse` and `OpenFoodFactsProduct` source-gen DTOs (`[JsonSerializable]` in `JsonSerializerContext`)
- [ ] 3.4 Implement `OpenFoodFactsProductLookupService`: GET `https://world.openfoodfacts.org/api/v2/product/{barcode}.json`, deserialize, map to `ProductInfo?`
- [ ] 3.5 Configure `HttpClient` for Open Food Facts: set descriptive `User-Agent`, timeout defaults to 5 s (override via `OFF_TIMEOUT_SECONDS` env var)
- [ ] 3.6 Register `IProductLookupService` → `OpenFoodFactsProductLookupService` via `IHttpClientFactory` in DI

## 4. Barcode Session Store

- [ ] 4.1 Create `BarcodeSession` record: `string Barcode`, `string ItemName`, `DateTimeOffset ExpiresAt`
- [ ] 4.2 Implement `BarcodeSessionService`: thread-safe in-memory store, `string CreateSession(BarcodeSession)` returns 6-char random token, `BarcodeSession? GetSession(string token)` returns null if expired or missing, lazy cleanup on access
- [ ] 4.3 Register `BarcodeSessionService` as singleton in DI

## 5. Photo Message Handler

- [ ] 5.1 Define `IPhotoMessageHandler` interface with `Task HandleAsync(ITelegramBotClient bot, Message message, CancellationToken ct)`
- [ ] 5.2 Implement `BarcodePhotoHandler`: check private chat, check file size ≤ 10 MB, download largest photo, call `IBarcodeDecoder`, call `IProductLookupService`, create session token, reply with product info + inline button (or fallback barcode reply), call `IHistoryRepository.RecordAsync` wrapped in try/catch
- [ ] 5.3 Wire photo routing in `UpdateHandler`: detect `update.Message?.Photo != null` in private chat, dispatch to `IPhotoMessageHandler`
- [ ] 5.4 Register `IPhotoMessageHandler` → `BarcodePhotoHandler` as transient in DI

## 6. Barcode Add Callback Handler

- [ ] 6.1 Implement `BarcodeAddCallbackHandler` with `CallbackPrefix = "barcode_add"`: parse token from callback data, retrieve session, invoke item-search pre-filled entry point with `ItemName`, reply with `session_expired` if session missing or expired
- [ ] 6.2 Register `BarcodeAddCallbackHandler` as a callback handler in DI

## 7. Item-Search Pre-filled Entry Point

- [ ] 7.1 Expose a public method (or overload) in the item-search handler/service that accepts a pre-filled search term and skips the "type item name" prompt, returning results immediately
- [ ] 7.2 Ensure the pre-filled entry point reuses all existing item-search logic (result formatting, selection, adding to list) unchanged

## 8. Localization Keys

- [ ] 8.1 Add key `barcode_not_found_in_photo` to all locale files (e.g., "No barcode found in the photo. Try a clearer image.")
- [ ] 8.2 Add key `photo_too_large` (e.g., "Photo is too large to process. Please send a smaller image.")
- [ ] 8.3 Add key `product_found` with placeholders for product name, brand, quantity
- [ ] 8.4 Add key `product_not_found_barcode` with placeholder for the raw barcode value
- [ ] 8.5 Add key `add_as_item` (button label, e.g., "Add as item")
- [ ] 8.6 Add key `session_expired` (e.g., "This action has expired. Please send the photo again.")

## 9. Unit Tests

- [ ] 9.1 Unit test `BarcodeDecoder`: mock image bytes containing a known EAN-13 barcode → assert decoded value matches; test with no-barcode image → assert null returned
- [ ] 9.2 Unit test `OpenFoodFactsProductLookupService`: mock `HttpClient` returning `status:1` response → assert `ProductInfo` mapped correctly; mock `status:0` → assert null; mock timeout → assert null and Warning logged
- [ ] 9.3 Unit test `BarcodeSessionService`: create session → retrieve within TTL → assert found; retrieve after TTL → assert null; verify expired sessions cleaned up
- [ ] 9.4 Unit test `BarcodePhotoHandler`: photo in group chat → assert ignored; photo > 10 MB → assert `photo_too_large` reply; no barcode decoded → assert `barcode_not_found_in_photo` reply; product found → assert `product_found` reply with inline button; product not found → assert `product_not_found_barcode` reply; history recording failure → assert reply still sent
- [ ] 9.5 Unit test `BarcodeAddCallbackHandler`: valid token within TTL → assert item-search pre-fill called with correct name; expired/missing token → assert `session_expired` reply

## 10. Docker / AOT Validation

- [ ] 10.1 Build Docker image with `dotnet publish -r linux-x64 --self-contained` and verify the image starts without `TypeLoadException` or trim warnings related to ZXing.Net
- [ ] 10.2 Verify SkiaSharp native libraries are present and load correctly inside the Docker container
- [ ] 10.3 Send a test photo with a barcode through the running Docker container and confirm end-to-end decode + Open Food Facts reply
