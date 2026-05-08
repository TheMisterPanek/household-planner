## Why

Users frequently want to add purchased or tracked items by scanning a product barcode, but manually typing product names is error-prone and slow. A photo-based barcode reader that looks up the Open Food Facts / Open Barcode Database gives users an instant, accurate product name without relying on AI APIs or paid services.

## What Changes

- New Telegram handler: user sends or forwards a photo containing a barcode; the bot replies with product name and any available metadata (brand, quantity).
- Barcode decoding is done locally using a .NET-compatible OSS library (ZXing.Net or similar) — no AI, no third-party vision API.
- Product metadata is fetched from the Open Food Facts REST API (`https://world.openfoodfacts.org/api/v2/product/{barcode}.json`) — free, no auth required.
- Decoded barcode value is also offered as a pre-filled item name for the shopping list or product tracker, so the user can confirm or edit before saving.

## Capabilities

### New Capabilities
- `barcode-scanning`: Decode EAN-8, EAN-13, UPC-A, UPC-E, Code-128 barcodes from a Telegram photo using a local library (no AI).
- `product-lookup-by-barcode`: Fetch product name, brand, and quantity from the Open Food Facts API using a decoded barcode; fall back gracefully when the product is not found.

### Modified Capabilities
- `item-search`: When a barcode lookup succeeds, allow the returned product name to be used as the pre-filled search/add term in the item-search flow.

## Impact

- **New dependencies**: ZXing.Net (AOT-compatible build or reflection-free path must be validated); `System.Net.Http.HttpClient` registered via DI for Open Food Facts.
- **Telegram**: New photo message handler (`IDialogMessageHandler` or a dedicated photo handler). Must download the file via Telegram Bot API, decode the barcode in-memory, and release the stream.
- **Localization**: New keys for "barcode not found in photo", "product not found in database", "barcode found – product: {name}", "use as item name?".
- **AOT**: ZXing.Net must be trimming-safe or wrapped to avoid reflection. Photo download uses `HttpClient` which is AOT-safe.
- **Affected teams**: Only this project (single-team, single-repo).
- **Rollback plan**: Feature is isolated behind the photo message handler. Removing the handler registration and its dependencies restores the prior behaviour with no data-migration needed. No schema changes.
- **Cross-cutting decisions relied on**:
  - `HttpClient` registered via DI — consistent with standard `IHttpClientFactory` use.
  - Localization through `ILocalizer.Get(chatId, key)` — all user-facing strings go through it.
  - History audit: successful barcode-lookup actions recorded via `IHistoryRepository.RecordAsync`.
  - No deviation from existing cross-cutting architecture.
