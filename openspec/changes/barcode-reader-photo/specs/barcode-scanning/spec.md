## ADDED Requirements

### Requirement: Bot decodes barcodes from Telegram photos
The system SHALL implement a photo message handler (`IPhotoMessageHandler`) that is invoked whenever an incoming Telegram update contains a `Message.Photo` payload in a private chat. The handler SHALL download the largest available photo resolution from Telegram (up to a configurable cap of 10 MB), decode the image bytes using a local barcode library (ZXing.Net + SkiaSharp), and reply with the decoded barcode value or an appropriate error. No AI or external vision API SHALL be used.

#### Scenario: Photo contains a readable barcode
- **WHEN** a user sends a photo containing a valid EAN-13 barcode (e.g., `4006381333931`) in a private chat
- **THEN** the bot successfully decodes the barcode and proceeds to product lookup

#### Scenario: Photo contains no recognisable barcode
- **WHEN** a user sends a photo with no barcode (or a barcode too blurry to decode)
- **THEN** the bot replies with the localized `barcode_not_found_in_photo` message and logs a Warning

#### Scenario: Photo file exceeds size cap
- **WHEN** the Telegram file_size field for the photo exceeds 10 MB
- **THEN** the bot replies with the localized `photo_too_large` message without downloading the file

#### Scenario: Photo received in a group chat
- **WHEN** a user sends a barcode photo inside a group chat
- **THEN** the bot ignores the photo (no reply), consistent with v1 private-only scope

### Requirement: Supported barcode formats
The system SHALL decode the following barcode symbologies: EAN-8, EAN-13, UPC-A, UPC-E, Code-128. Other formats (QR Code, Data Matrix, PDF417, etc.) are out of scope and SHALL NOT be required to decode.

#### Scenario: EAN-13 barcode decoded successfully
- **WHEN** the photo contains a clear EAN-13 barcode
- **THEN** the handler returns the 13-digit barcode string

#### Scenario: Code-128 barcode decoded successfully
- **WHEN** the photo contains a clear Code-128 barcode
- **THEN** the handler returns the barcode string

#### Scenario: QR code in photo
- **WHEN** the photo contains only a QR code
- **THEN** the bot replies with the localized `barcode_not_found_in_photo` message (QR codes are not supported)

### Requirement: Barcode scan recorded in audit history
After a successful barcode decode (regardless of whether the product lookup succeeds), the system SHALL call `IHistoryRepository.RecordAsync` with action type `barcode_scan` and the decoded barcode value. The call SHALL be wrapped in try/catch — a failure to record history MUST NOT suppress the user-facing response.

#### Scenario: History recorded after successful decode
- **WHEN** a barcode is successfully decoded from a photo
- **THEN** `IHistoryRepository.RecordAsync` is called with `barcode_scan` and the barcode value

#### Scenario: History recording fails
- **WHEN** `IHistoryRepository.RecordAsync` throws an exception
- **THEN** the exception is caught, logged at Warning, and the user-facing reply is sent normally
