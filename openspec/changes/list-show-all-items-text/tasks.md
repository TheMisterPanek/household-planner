## 1. Update ShoppingListService

- [ ] 1.1 Modify `BuildListAsync()` method to format items as a bullet-point text list before buttons
- [ ] 1.2 Extract item list formatting logic into a helper method (e.g., `FormatItemsAsText()`)
- [ ] 1.3 Handle pagination: text list should show only items on the current page
- [ ] 1.4 Include item quantity in parentheses if provided (e.g., "• Milk (2L)")
- [ ] 1.5 Display item name only if no quantity (e.g., "• Bread")
- [ ] 1.6 Ensure message stays under 4096 characters with all content (text + buttons)

## 2. Localization Keys

- [ ] 2.1 Add English localization key `list.item-format` (or similar) for item display template if needed
- [ ] 2.2 Add Russian localization key equivalent
- [ ] 2.3 Add Polish localization key equivalent
- [ ] 2.4 Verify localization keys in Strings.en.json, Strings.ru.json, Strings.pl.json

## 3. Unit Tests

- [ ] 3.1 Test: `BuildListAsync` returns message with bullet list for items on single page
- [ ] 3.2 Test: Bullet list includes item quantities when provided
- [ ] 3.3 Test: Bullet list shows item names only when quantity is null
- [ ] 3.4 Test: `BuildListAsync` respects pagination (shows only current page items in text)
- [ ] 3.5 Test: Message with large list of items stays under 4096 characters
- [ ] 3.6 Test: Empty list message has no bullet items
- [ ] 3.7 Test: Buttons are still rendered after bullet list text

## 4. Integration Testing

- [ ] 4.1 Manual test: `/list` command displays all items as bullet list in message body
- [ ] 4.2 Manual test: Items with quantities display correctly in list
- [ ] 4.3 Manual test: Pagination buttons and text list show correct page items
- [ ] 4.4 Manual test: List message length is acceptable for Telegram limits
- [ ] 4.5 Manual test: Localization works correctly for different language preferences

## 5. Code Review & Cleanup

- [ ] 5.1 Verify no hardcoded strings in handlers (all localized)
- [ ] 5.2 Check test coverage meets project standards
- [ ] 5.3 Ensure code builds with no new warnings
- [ ] 5.4 Verify backward compatibility with existing list functionality
