## ADDED Requirements

### Requirement: Product metadata fetched from Open Food Facts
After a barcode is successfully decoded, the system SHALL call `IProductLookupService.LookupAsync(barcode)` which queries the Open Food Facts REST API (`https://world.openfoodfacts.org/api/v2/product/{barcode}.json`). The service SHALL use `IHttpClientFactory` and `System.Text.Json` source-generated deserialization. The HTTP timeout SHALL default to 5 seconds and SHALL be overridable via the `OFF_TIMEOUT_SECONDS` environment variable. The service SHALL set a descriptive `User-Agent` header.

#### Scenario: Product found in Open Food Facts
- **WHEN** the barcode `4006381333931` is decoded and Open Food Facts returns `status: 1`
- **THEN** `IProductLookupService.LookupAsync` returns a `ProductInfo` with `ProductName`, `Brand`, and `Quantity` populated

#### Scenario: Product not found in Open Food Facts
- **WHEN** the decoded barcode has no match in Open Food Facts (API returns `status: 0`)
- **THEN** `IProductLookupService.LookupAsync` returns `null` (or a "not found" result)

#### Scenario: Open Food Facts API timeout
- **WHEN** the HTTP request to Open Food Facts exceeds the configured timeout
- **THEN** the exception is caught, logged at Warning, and the service returns null (not-found path)

#### Scenario: Open Food Facts API returns non-2xx response
- **WHEN** the API returns a 5xx or 4xx response
- **THEN** the exception is caught, logged at Warning, and the service returns null

### Requirement: Bot replies with product info when found
When `IProductLookupService.LookupAsync` returns a product, the system SHALL reply to the user with the localized `product_found` message containing the product name, brand, and quantity. The reply SHALL include an inline keyboard button labelled with the localized `add_as_item` key. The button callback data SHALL use the session-token pattern (prefix `barcode_add:` + 6-char random token; total ≤ 14 bytes) to avoid the 64-byte callback data limit.

#### Scenario: Product found — reply with details and inline button
- **WHEN** `IProductLookupService.LookupAsync` returns product name "Coca-Cola", brand "Coca-Cola Co.", quantity "330 ml"
- **THEN** the bot replies with a message containing "Coca-Cola (Coca-Cola Co., 330 ml)" and an "Add as item" inline button

#### Scenario: Product found but brand and quantity are absent
- **WHEN** `IProductLookupService.LookupAsync` returns only product name with no brand or quantity
- **THEN** the bot replies with the product name only (omitting the parenthetical)

### Requirement: Bot offers barcode as fallback item name when product not found
When `IProductLookupService.LookupAsync` returns no product, the system SHALL reply with the localized `product_not_found_barcode` message (including the raw barcode value) and SHALL include an inline keyboard button offering to use the raw barcode string as the item name. This allows the user to still record the item even when the database has no entry.

#### Scenario: Product not found — offer raw barcode as item name
- **WHEN** the barcode `9999999999999` is decoded and no product is found
- **THEN** the bot replies with "Product not found. Barcode: 9999999999999" and an "Use barcode as item name" inline button

### Requirement: Barcode session expires after 10 minutes
The in-memory session storing `{ barcode, productName }` keyed by a random token SHALL have a TTL of 10 minutes. After expiry, tapping the "Add as item" button SHALL result in a localized `session_expired` error reply. Sessions SHALL be cleaned up lazily (on access) or by a background sweep.

#### Scenario: User taps "Add as item" within 10 minutes
- **WHEN** the user taps the inline button within 10 minutes of receiving the product reply
- **THEN** the session is found and the item-search flow is pre-filled with the product name

#### Scenario: User taps "Add as item" after session expiry
- **WHEN** the user taps the inline button more than 10 minutes after it was issued
- **THEN** the bot replies with the localized `session_expired` message
