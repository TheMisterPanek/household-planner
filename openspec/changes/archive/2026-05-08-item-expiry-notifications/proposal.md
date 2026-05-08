## Why

Items in the shopping list have no visibility into whether products are still fresh or close to expiry, leading to waste or missed restocking. A daily digest per group chat surfaces what has already expired and what is expiring soon, so the household can act before it's too late.

## What Changes

- `ShoppingItem` gets an optional `ExpDate` (`DateOnly?`) column stored in SQLite.
- The `/buy` command flow gains an optional expiry-date step after quantity (with a skip button).
- A background-scheduled job runs once per day and sends an expiry summary to every registered group that has items with upcoming or past expiration dates.
- The `/list` command display shows the expiry date next to items that have one.

## Capabilities

### New Capabilities

- `item-expiry`: Storing and displaying an optional expiration date on each shopping item, including the extended `/buy` dialog step and updated `/list` rendering.
- `expiry-notifications`: Daily background job that queries all groups, classifies items by expiry status (expired today, expiring in ≤3 days, expiring in ≤7 days), and posts a formatted summary message to each qualifying group chat.

### Modified Capabilities

- `shopping-list`: The `/buy` dialog adds an optional expiry-date input step; the `/list` display includes expiry dates next to items that have them.

## Impact

- **Database**: `shopping_items` table gets a new nullable `exp_date` column (TEXT, ISO-8601 date); migration via `DatabaseInitializer`.
- **Models**: `ShoppingItem` record gains `ExpDate` (`DateOnly?`) property.
- **Handlers**: `BuyStepHandler` extended with a new dialog state for expiry date input.
- **Services**: `ShoppingListService` / `ShoppingItemRepository` updated to persist and query `exp_date`; new `ExpiryNotificationService` with query logic and message formatting.
- **Scheduling**: New `ExpiryNotificationJob` hosted service (or `IHostedService` with a daily timer) registered in DI.
- **Rollback**: The `exp_date` column is nullable, so removing the notification job and reverting handler code restores prior behaviour without a data migration.
- **Teams affected**: Bot functionality only; no external API or infrastructure changes beyond the existing SQLite database and Telegram bot token.
