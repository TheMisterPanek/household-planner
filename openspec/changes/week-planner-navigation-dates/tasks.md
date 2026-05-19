## 1. Schema Migration

- [x] 1.1 In `DatabaseInitializer.StartAsync`, add migration block to add `WeekStartDate TEXT NOT NULL DEFAULT ''` column to `DayMeals` using `PRAGMA table_info(DayMeals)` check + `ALTER TABLE DayMeals ADD COLUMN WeekStartDate TEXT NOT NULL DEFAULT ''`; catch `SqliteException` with error code 1 (duplicate column) and ignore.

## 2. Update DayMealsRepository

- [x] 2.1 Add `weekStartDate` parameter to `GetWeekAsync(int groupId, string weekStartDate)` — add `AND dm.WeekStartDate = @weekStartDate` to WHERE clause.
- [x] 2.2 Add `weekStartDate` parameter to `InsertAsync(int groupId, int dayOfWeek, int mealId, string weekStartDate)` — include in INSERT.
- [x] 2.3 Add `weekStartDate` parameter to `GetCountAsync(int groupId, int dayOfWeek, string weekStartDate)` — add to WHERE clause.
- [x] 2.4 Add `weekStartDate` parameter to `ClearAsync(int groupId, int dayOfWeek, string weekStartDate)` — add to WHERE clause.
- [x] 2.5 Add `weekStartDate` parameter to `ClearMealAsync(int groupId, int dayOfWeek, int mealId, string weekStartDate)` — add to WHERE clause.

## 3. Add Localization Keys

- [x] 3.1 Add to `Strings.en.json`:
  - `week.btn-prev-week`: `"← {0}"`
  - `week.btn-next-week`: `"{0} →"`
  - `week.day-header`: `"📅 {0}, {1}:"`
  - `week.month.1`…`week.month.12`: `"Jan"`, `"Feb"`, `"Mar"`, `"Apr"`, `"May"`, `"Jun"`, `"Jul"`, `"Aug"`, `"Sep"`, `"Oct"`, `"Nov"`, `"Dec"`
- [x] 3.2 Add the same keys to `Strings.ru.json` with Russian month abbreviations: `"янв"`, `"фев"`, `"мар"`, `"апр"`, `"май"`, `"июн"`, `"июл"`, `"авг"`, `"сен"`, `"окт"`, `"ноя"`, `"дек"`.
- [x] 3.3 Add the same keys to `Strings.pl.json` with Polish month abbreviations: `"sty"`, `"lut"`, `"mar"`, `"kwi"`, `"maj"`, `"cze"`, `"lip"`, `"sie"`, `"wrz"`, `"paź"`, `"lis"`, `"gru"`.

## 4. Update WeekViewBuilder

- [x] 4.1 Add static helper `GetWeekMonday(DateOnly date) → DateOnly` — subtracts `((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7` days.
- [x] 4.2 Add static helper `FormatWeekRange(DateOnly monday, ILocalizer localizer, long chatId) → string` — formats as `"May 18–24, 2026"` (same month) or `"May 29 – Jun 4, 2026"` (cross-month), using `week.month.{n}` keys.
- [x] 4.3 Add static helper `FormatDayDate(DateOnly monday, int dayOfWeek, ILocalizer localizer, long chatId) → string` — returns `"{MonthAbbr} {day}"` for the specific day (e.g., `"May 18"`).
- [x] 4.4 Update `BuildDayListKeyboard(ILocalizer localizer, long chatId, DateOnly monday)` — add `monday` parameter; update all 7 day button callbacks to `week:day:{weekStart}:{day}`; add bottom nav row with prev (`week:nav:{prevMonday}`) and next (`week:nav:{nextMonday}`) buttons labelled via `week.btn-prev-week` / `week.btn-next-week` with `FormatWeekRange`.
- [x] 4.5 Update `BuildDayDetailKeyboard(int day, IReadOnlyList<DayMealEntry> dayMeals, bool hasMeals, ILocalizer localizer, long chatId, string weekStartDate)` — add `weekStartDate` parameter; update all callbacks to embed `weekStartDate` (e.g., `week:clear:{weekStart}:{day}:{mealId}`, `week:pick:{weekStart}:{day}`, `week:to_cart:{weekStart}:{day}`, `week:back:{weekStart}`).
- [x] 4.6 Update `BuildWeekSummaryText` — accept `DateOnly monday` and append the formatted week range to the header line.

## 5. Update WeekCommandHandler

- [x] 5.1 Compute `monday = WeekViewBuilder.GetWeekMonday(DateOnly.FromDateTime(DateTime.UtcNow))` and pass as string `monday.ToString("yyyy-MM-dd")` to `DayMealsRepository.GetWeekAsync` and `WeekViewBuilder.BuildDayListKeyboard`.

## 6. Update WeekCallbackHandler

- [x] 6.1 Add `nav` action: parse `weekStart` from `parts[2]`, convert to `DateOnly`, call `RenderDayListViewAsync` for that date.
- [x] 6.2 Update `day` action: parse `weekStart` from `parts[2]`, day from `parts[3]`; pass to `RenderDayDetailViewAsync`.
- [x] 6.3 Update `pick` action: parse `weekStart` from `parts[2]`, day from `parts[3]`; embed `weekStart` in `week:assign:{weekStart}:{day}:{mealId}` and back button `week:day:{weekStart}:{day}`.
- [x] 6.4 Update `assign` action: parse `weekStart` from `parts[2]`, day from `parts[3]`, mealId from `parts[4]`; pass `weekStart` to `InsertAsync` and `RenderDayDetailViewAsync`.
- [x] 6.5 Update `clear` action: parse `weekStart` from `parts[2]`, day from `parts[3]`, mealId from `parts[4]`; pass `weekStart` to `ClearMealAsync` and `RenderDayDetailViewAsync`.
- [x] 6.6 Update `to_cart` action: parse `weekStart` from `parts[2]`, day from `parts[3]`; pass `weekStart` to `GetWeekAsync`.
- [x] 6.7 Update `back` action: parse `weekStart` from `parts[2]`; pass to `RenderDayListViewAsync`.
- [x] 6.8 Update `RenderDayListViewAsync` to accept `DateOnly monday`; use for repository query and keyboard build.
- [x] 6.9 Update `RenderDayDetailViewAsync` to accept `string weekStartDate`; pass to repository queries and keyboard builder; update day header to include formatted date via `FormatDayDate`.

## 7. Unit Tests — WeekViewBuilder

- [x] 7.1 `GetWeekMonday` — Monday input returns self; Wednesday input returns that week's Monday; Sunday input returns that week's Monday.
- [x] 7.2 `FormatWeekRange` — same-month week formats as `"May 18–24, 2026"`; cross-month week formats as `"May 29 – Jun 4, 2026"`.
- [x] 7.3 `BuildDayListKeyboard` — nav row last; prev button data is `week:nav:{prevMonday}`; next button data is `week:nav:{nextMonday}`; day button data includes week start.
- [x] 7.4 `BuildDayDetailKeyboard` — all callbacks contain `weekStartDate`.
- [x] 7.5 `BuildWeekSummaryText` — header includes week range string.

## 8. Unit Tests — WeekCommandHandler

- [x] 8.1 `/week` sends keyboard with 7 day buttons and a nav row.
- [x] 8.2 `DayMealsRepository.GetWeekAsync` is called with current week's Monday date string (not empty string).

## 9. Unit Tests — WeekCallbackHandler

- [x] 9.1 `week:nav:2026-05-25` renders week view for May 25 week.
- [x] 9.2 `week:day:2026-05-18:1` renders day detail for Monday May 18.
- [x] 9.3 `week:assign:2026-05-18:1:{mealId}` calls `InsertAsync` with `weekStartDate = "2026-05-18"`.
- [x] 9.4 `week:clear:2026-05-18:1:{mealId}` calls `ClearMealAsync` with `weekStartDate = "2026-05-18"`.
- [x] 9.5 `week:to_cart:2026-05-18:1` queries meals with `weekStartDate = "2026-05-18"`.
- [x] 9.6 `week:back:2026-05-18` renders day list for May 18 week.

## 10. Integration Tests

- [x] 10.1 `/week` in group → message contains week range (e.g. current Monday date substring) in text and keyboard has 9 buttons (7 days + 2 nav).
- [x] 10.2 `week:nav:2026-05-25` callback → edits message to show May 25 week.
- [x] 10.3 `week:day:2026-05-18:2` callback → edits to day detail for Tuesday May 19.
- [x] 10.4 `week:assign:2026-05-18:2:{mealId}` → persists with `WeekStartDate = "2026-05-18"`, shows day detail.
- [x] 10.5 `week:clear:2026-05-18:2:{mealId}` → removes scoped row, shows day detail.
- [x] 10.6 `week:back:2026-05-18` → returns to week list for May 18 week.

## 11. Smoke Test (manual — do not mark complete without user confirmation)

- [ ] 11.1 `/week` in group → header shows current week's date range (e.g. "📅 Meal plan: May 18–24, 2026"), 7 day buttons visible, prev/next nav buttons at bottom.
- [ ] 11.2 Tap `← prev` button → view switches to previous week, header updates to previous week's range.
- [ ] 11.3 Tap `next →` button from previous week → returns to original week.
- [ ] 11.4 Navigate two weeks forward → assign a meal to a day → tap Back → verify meal appears for that future week.
- [ ] 11.5 Navigate back to current week → confirm that future week meal does NOT appear here (weeks are isolated).
- [ ] 11.6 Tap a day button → day header shows date (e.g., "📅 Monday, May 18:").
- [ ] 11.7 Full flow: navigate to next week → tap a day → add meal → back → back → verify week summary is correct for next week.
- [ ] 11.8 Navigate to previous week → view old assignments (if any) are separate from current week.
