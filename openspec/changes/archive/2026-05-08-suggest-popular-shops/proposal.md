## Why

Users frequently purchase from the same stores repeatedly (e.g., "Carrefour", "Stokrotka"). Typing the store name every time introduces friction and typos. Suggesting the 5 most popular shops from the user's purchase history streamlines the price-capture flow and reduces data entry errors.

## What Changes

- When the bot asks "📍 Where did you buy {item}?" in the price-capture dialog (step 1), display inline buttons with the 5 most frequently used shop names from that user's purchase history
- User can tap a button to select a suggested shop, or type a custom shop name
- If the user has fewer than 5 prior purchases with shop names, show only those that exist
- The shop suggestions are scoped per user per group (each user sees their own shopping history)

## Capabilities

### New Capabilities
- `shop-suggestions`: Suggest popular shop names as inline buttons during price-capture dialog step 1. Ranks shops by frequency of prior purchases.

### Modified Capabilities
- `purchase-history`: Add `GetTopShopsAsync(int groupId, long userId, int limit)` to retrieve the top N shop names by purchase frequency for a user

## Impact

**Code**:
- `PurchaseHistoryRepository`: Add `GetTopShopsAsync` method
- `PriceCaptureStepHandler`: Modify step 1 to fetch top shops and display them as inline buttons alongside skip button
- `Program.cs`: No registration changes needed

**Database**: No schema changes — shops are already stored in the `StoreName` column of `PurchaseHistory`

**APIs**: Internal only (no external APIs affected)

**Localization**: New message keys if shop button labels or related text is localized

**Cross-cutting**: Follows existing patterns: `IDialogMessageHandler` for step handling, `Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton` for button rendering, per-user scoping via `(chatId, userId)` keys
