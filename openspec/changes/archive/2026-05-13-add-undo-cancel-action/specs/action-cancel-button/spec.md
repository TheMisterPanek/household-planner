## ADDED Requirements

### Requirement: Destructive action messages include an inline Cancel button
The system SHALL append a Cancel inline button as the last row of the `InlineKeyboardMarkup` on every bot message that triggers a destructive action. Destructive actions include: mark item as bought, remove item from list, archive shopping list, and merge meal into shopping list. The Cancel button callback data SHALL use the format `action:cancel` (12 bytes, within the 64-byte limit).

#### Scenario: Cancel button appears on mark-item-done message
- **WHEN** the bot sends a message with a "mark as done" inline button
- **THEN** a Cancel button row is included at the bottom of the keyboard

#### Scenario: Cancel button appears on archive-list message
- **WHEN** the bot sends a message with an "archive list" inline button
- **THEN** a Cancel button row is included at the bottom of the keyboard

#### Scenario: Cancel button appears on remove-item message
- **WHEN** the bot sends a message with a "remove item" inline button
- **THEN** a Cancel button row is included at the bottom of the keyboard

#### Scenario: Cancel button appears on meal-merge message
- **WHEN** the bot sends a message with a "merge meal to list" inline button
- **THEN** a Cancel button row is included at the bottom of the keyboard

---

### Requirement: Pressing Cancel removes the action message or its keyboard
The system SHALL register an `ActionCancelCallbackHandler` keyed to `CallbackPrefix = "action:cancel"`. When invoked, it SHALL attempt to delete the original message via `DeleteMessageAsync`. If deletion is not permitted (e.g., `ApiRequestException` for group permission restrictions), it SHALL fall back to removing the inline keyboard via `EditMessageReplyMarkupAsync(null)`. No state change SHALL occur; the action is simply abandoned.

#### Scenario: Cancel deletes the message successfully
- **WHEN** a user taps the Cancel button and the bot has delete permission
- **THEN** the original action message is deleted and no action is executed

#### Scenario: Cancel falls back to keyboard removal when delete is forbidden
- **WHEN** a user taps the Cancel button and `DeleteMessageAsync` throws `ApiRequestException`
- **THEN** the bot removes the inline keyboard from the message via `EditMessageReplyMarkupAsync(null)` and no action is executed

#### Scenario: Cancel does not execute the intended action
- **WHEN** a user taps Cancel on any destructive action message
- **THEN** the underlying action (item bought, archive, remove, merge) is NOT applied and no history entry is written

#### Scenario: Cancel button label is localized
- **WHEN** the Cancel button is rendered in any message
- **THEN** its label text is resolved via `ILocalizer.Get(chatId, "action.cancel")` and reflects the chat's configured language
