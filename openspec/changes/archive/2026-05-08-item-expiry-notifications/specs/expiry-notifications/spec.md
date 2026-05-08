## ADDED Requirements

### Requirement: Daily expiry summary notification to all registered group chats
The system SHALL run a background job once per day at a configurable UTC time (default 09:00, via `NOTIFY_TIME_UTC` env var) and send an expiry summary message to every registered group chat that has at least one item whose `exp_date` falls into a reportable category. Groups with no items with `exp_date` set, or whose items all expire more than 7 days out, SHALL receive no message.

#### Scenario: Group has expired items
- **WHEN** the daily job runs and a group has items with `exp_date < today`
- **THEN** the bot sends a message to that group listing those items under the heading "🔴 Просроченные:"

#### Scenario: Group has items expiring today
- **WHEN** the daily job runs and a group has items with `exp_date == today`
- **THEN** the bot sends a message listing those items under the heading "🟡 Истекает сегодня:"

#### Scenario: Group has items expiring in 1–3 days
- **WHEN** the daily job runs and a group has items with `today < exp_date <= today + 3 days`
- **THEN** the bot sends a message listing those items with their date under the heading "🟠 Истекает скоро (до 3 дней):"

#### Scenario: Group has items expiring in 4–7 days
- **WHEN** the daily job runs and a group has items with `today + 3 days < exp_date <= today + 7 days`
- **THEN** the bot sends a message listing those items with their date under the heading "📅 Истекает на этой неделе (4–7 дней):"

#### Scenario: Group has items in multiple categories
- **WHEN** the daily job runs and a group has items in more than one category
- **THEN** the bot sends a single message containing all applicable sections in order: expired → today → soon → this week

#### Scenario: Group has no items with expiry dates or all beyond 7 days
- **WHEN** the daily job runs and a group has no items with `exp_date` set, or all set items expire more than 7 days out
- **THEN** no message is sent to that group

#### Scenario: Notification job fires at configured time
- **WHEN** the `NOTIFY_TIME_UTC` env var is set to "08:30"
- **THEN** the job fires at 08:30 UTC daily (first fire may be the next occurrence after startup)

#### Scenario: Telegram send fails for one group
- **WHEN** the daily job runs and sending the notification to one group throws an exception
- **THEN** the error is logged at Warning level with the group's `ChatId`, and the job continues processing remaining groups

---

### Requirement: Notification message format
The system SHALL format each daily notification as a structured summary with a header and per-category item lists.

#### Scenario: Notification message header
- **WHEN** the bot sends a daily notification
- **THEN** the message begins with the header "📋 Сводка по срокам годности:"

#### Scenario: Item line format with quantity
- **WHEN** an item with a quantity appears in a notification category
- **THEN** it is rendered as "• Name Qty (DD.MM.YYYY)" where the date is in `dd.MM.yyyy` format

#### Scenario: Item line format without quantity
- **WHEN** an item without a quantity appears in a notification category
- **THEN** it is rendered as "• Name (DD.MM.YYYY)"

#### Scenario: Expired item line omits date
- **WHEN** an expired item appears in the notification
- **THEN** it is rendered as "• Name Qty" without a date (the category heading already implies it is past due)
