# Requirements Document

## Introduction

The Dish Statistics feature adds two detail pages — DishDetailsPage and HousemateDetailsPage — that provide visual analytics on saved dish usage and cooking attribution within a household. Users can explore how often dishes are cooked, who is cooking them, cooking streaks, and cooking share distribution through interactive timeline charts and summary statistics. Data is derived from DayPlanDishLinks (which dishes were used on which days) combined with AttendanceRecords (who was chef on each day). A configurable time range controls summary statistics, while timeline charts are independently scrollable with infinite scroll-back.

## Glossary

- **Statistics_Engine**: The server-side component responsible for computing dish usage frequency, cooking attribution, streaks, and ratios from DayPlanDishLink and AttendanceRecord data.
- **DishDetailsPage**: The page at `/saved-dishes/{id}` showing statistics for a specific saved dish.
- **HousemateDetailsPage**: The page at `/housemates/{id}` showing statistics for a specific housemate.
- **Time_Range_Selector**: A UI control with quick-tap pills (30d, 3mo, 1yr, all-time) that determines the date window for summary statistics.
- **Timeline_Chart**: A horizontally scrollable chart with entities on the vertical axis and time on the horizontal axis, showing dots per cooking day.
- **Chef_Attribution**: The determination of which housemate(s) cooked on a given day, derived from AttendanceRecord entries where IsChef is true.
- **Cooking_Day**: A day on which a specific saved dish was linked to the day plan (via DayPlanDishLink) and the referenced SavedDish has IsDeleted equal to false. Chef attribution is not required — a dish on the plan counts regardless of whether any housemate was marked as chef.
- **Cook_Ratio**: The proportion of a housemate's eating-in days on which the housemate was marked as chef.
- **Cooking_Streak**: The longest consecutive sequence of days on which a housemate was marked as chef.

## Requirements

### Requirement 1: DishDetailsPage Navigation and Layout

**User Story:** As a housemate, I want to tap a saved dish and see detailed statistics for that dish, so that I can understand how frequently and by whom it is cooked.

#### Acceptance Criteria

1. THE SavedDishesPage SHALL display a statistics icon button alongside the existing edit and delete action buttons for each saved dish.
2. WHEN the user taps the statistics icon button for a saved dish, THE application SHALL navigate to the DishDetailsPage at route `/saved-dishes/{id}` where `{id}` is the saved dish's GUID.
3. THE SavedDishesPage SHALL keep the existing edit and delete action buttons on each dish row; these buttons SHALL NOT navigate to the DishDetailsPage.
4. THE DishDetailsPage SHALL display the saved dish description as a visible heading at the top of the page.
5. THE DishDetailsPage SHALL include a Time_Range_Selector with quick-tap pills displayed in left-to-right order: 30 days, 3 months, 1 year, and all-time.
6. THE Time_Range_Selector SHALL default to 30 days on initial page load.
7. WHEN the user selects a different time range pill, THE DishDetailsPage SHALL display a loading indicator while fetching data and then display updated summary statistics for the selected range.
8. IF the user navigates to `/saved-dishes/{id}` where the id is not a valid GUID or does not match an existing non-deleted saved dish, THEN THE application SHALL redirect to the SavedDishesPage.

### Requirement 2: Dish Summary Statistics

**User Story:** As a housemate, I want to see at a glance how many times a dish was cooked and when it was last cooked, so that I can assess dish popularity and recency.

#### Acceptance Criteria

1. THE DishDetailsPage SHALL display a "times cooked" count showing the number of Cooking_Days for the dish within the selected time range, rendered at a larger font size than surrounding body text to serve as the primary statistic.
2. THE DishDetailsPage SHALL display an all-time "times cooked" count in a secondary style below the primary count, rendered at the standard body font size.
3. THE DishDetailsPage SHALL display a "last cooked" indicator based on the most recent Cooking_Day of the dish across all time (regardless of the selected time range): IF the most recent Cooking_Day is within the last 30 days, THEN the indicator SHALL show a relative label in the format "X days ago" (where X is the number of days between today and that Cooking_Day, and 0 days displays as "today"); otherwise the indicator SHALL show the absolute date formatted according to the user's active locale.
4. WHEN computing times cooked, THE Statistics_Engine SHALL count each day on which a DayPlanDishLink exists for the dish and the referenced SavedDish has IsDeleted equal to false, regardless of whether any housemate was marked as chef on that day.
5. IF the dish has zero Cooking_Days in the selected time range, THEN THE DishDetailsPage SHALL display zero as the times cooked count.
6. IF the dish has zero Cooking_Days across all time, THEN THE DishDetailsPage SHALL hide the "last cooked" indicator entirely.

### Requirement 3: Dish Timeline Chart

**User Story:** As a housemate, I want to see a visual timeline of who cooked a dish and when, so that I can spot patterns in dish usage across housemates.

#### Acceptance Criteria

1. THE DishDetailsPage SHALL display a Timeline_Chart with housemates on the vertical axis and time (day granularity) on the horizontal axis.
2. THE Timeline_Chart SHALL display one row per housemate who has cooked the dish at least once, excluding housemates who have never been chef on a day when the dish was used.
3. THE Timeline_Chart SHALL order housemate rows by ascending Housemate.SortOrder, matching the order used on the DayPlanPage.
4. THE Timeline_Chart SHALL display each housemate row using that housemate's assigned color.
5. THE Timeline_Chart SHALL display a dot or small marker for each day on which the housemate was chef and the dish was linked to the day plan.
6. THE Timeline_Chart SHALL be horizontally scrollable, with the right edge representing the current month.
7. WHEN the Timeline_Chart loads, THE system SHALL pre-load 3 months of timeline data ending at the current date (i.e., the current month and the 2 preceding months).
8. WHEN the user scrolls the Timeline_Chart to the left beyond the loaded data boundary, THE system SHALL fetch additional data in 1-month chunks (infinite scroll-back).
9. IF the system reaches the earliest available data point and no further historical data exists, THEN THE system SHALL stop requesting additional chunks and disable further scroll-back loading.
10. IF the dish has never been linked to any day plan where a housemate was chef, THEN THE Timeline_Chart SHALL display an empty state indicating that no cooking history is available for this dish.

### Requirement 4: HousemateDetailsPage Navigation and Layout

**User Story:** As a housemate, I want to tap a housemate and see their cooking statistics, so that I can understand cooking distribution and effort within the household.

#### Acceptance Criteria

1. THE HousematesPage SHALL display the following action buttons in left-to-right order for each housemate row: a rename (pencil) icon button, a color (palette) icon button, a statistics (chart) icon button, and a three-dot horizontal overflow menu icon button.
2. THE three-dot overflow menu icon SHALL use three horizontal dots (⋯) as its visual representation.
3. WHEN the user taps the three-dot overflow menu button, THE application SHALL display a dropdown menu containing the applicable reorder actions and the "Delete" action: for the first housemate in the list the menu SHALL contain "Move Down" and "Delete"; for the last housemate the menu SHALL contain "Move Up" and "Delete"; for all other housemates the menu SHALL contain "Move Up", "Move Down", and "Delete".
4. WHEN the user taps the statistics icon button for a housemate, THE application SHALL navigate to the HousemateDetailsPage at route `/housemates/{id}`.
5. THE HousematesPage SHALL keep the existing tap-to-switch-active-housemate behavior on the row itself; the statistics icon button and other action buttons SHALL NOT trigger the active housemate switch.
6. WHEN the user taps outside the overflow menu or performs any other action, THE overflow menu SHALL close.
7. THE HousemateDetailsPage SHALL display the housemate's name as an in-page heading visible on screen (the browser tab title remains "Happie" per application convention).
8. THE HousemateDetailsPage SHALL include a Time_Range_Selector with exactly four quick-tap pills labeled 30 days, 3 months, 1 year, and all-time, rendered in that order from left to right.
9. THE Time_Range_Selector SHALL default to the 30 days pill on initial page load and SHALL visually distinguish the currently active pill from the inactive pills.
10. WHEN the user selects a different time range pill, THE HousemateDetailsPage SHALL recompute and display updated summary statistics for the selected range.
11. IF the user navigates to `/housemates/{id}` where the id does not correspond to an existing housemate in the household, THEN THE application SHALL redirect to the HousematesPage.

### Requirement 5: Housemate Summary Statistics

**User Story:** As a housemate, I want to see my cooking frequency, eating-in days, cook ratio, longest streak, and busiest week, so that I can understand my contribution to household cooking.

#### Acceptance Criteria

1. THE HousemateDetailsPage SHALL display a "times cooked" count showing the total number of days the housemate had at least one AttendanceRecord with IsChef equal to true within the selected time range.
2. THE HousemateDetailsPage SHALL display an all-time "times cooked" count in smaller text below the primary count, where all-time includes all AttendanceRecords regardless of the selected time range.
3. THE HousemateDetailsPage SHALL display a "days eating in" count showing the number of days the housemate had AttendanceStatus equal to EatingIn within the selected time range.
4. THE HousemateDetailsPage SHALL display a Cook_Ratio label in the format "Cooked X of Y eating-in days" where X is the number of days the housemate was both marked as chef AND had AttendanceStatus equal to EatingIn, and Y is the total days eating in, within the selected range.
5. THE HousemateDetailsPage SHALL display the longest Cooking_Streak within the selected time range as a number accompanied by a flame icon, where the streak counts only consecutive days with IsChef equal to true that fall within the selected range boundaries.
6. THE HousemateDetailsPage SHALL display a "busiest week" statistic showing the highest number of days the housemate was chef within any single Monday-to-Sunday week that falls within the selected time range, displayed as a number (e.g., "5") with a label indicating it represents the most cooking days in one week.
7. IF the "times cooked" count within the selected time range equals zero, THEN THE HousemateDetailsPage SHALL display zero for the times cooked count, zero for days eating in, "Cooked 0 of 0 eating-in days" for Cook_Ratio, zero for the longest streak, and zero for the busiest week.

### Requirement 6: Share of Cooking Donut Chart

**User Story:** As a housemate, I want to see a visual breakdown of how cooking is distributed among all housemates, so that I can assess fairness in cooking responsibilities.

#### Acceptance Criteria

1. THE HousemateDetailsPage SHALL display a donut chart showing the share of cooking days for all non-deleted housemates within the selected time range, including housemates with zero cooking days.
2. THE donut chart SHALL use each housemate's assigned color for their segment.
3. THE donut chart SHALL visually distinguish the current housemate's segment (the housemate whose details page is being viewed) from other segments by rendering it with a visible offset (pulled out from the center) or increased stroke width compared to other segments.
4. THE donut chart SHALL display a percentage label per segment, calculated as that housemate's chef-day count divided by the total chef-day count across all non-deleted housemates, rounded to the nearest whole number.
5. WHEN computing share of cooking, THE Statistics_Engine SHALL count the number of days each non-deleted housemate was marked as chef within the selected time range.
6. IF multiple housemates were chef on the same day, THEN THE Statistics_Engine SHALL count that day for each housemate who was chef.
7. IF no housemate has any chef days within the selected time range, THEN THE donut chart SHALL not be displayed.

### Requirement 7: Top Dishes

**User Story:** As a housemate, I want to see which dishes I cook most often, so that I can identify my signature dishes and diversify my cooking.

#### Acceptance Criteria

1. THE HousemateDetailsPage SHALL display a "top dishes" section listing the saved dishes most frequently cooked by the housemate within the selected time range, showing a maximum of 10 dishes.
2. THE top dishes list SHALL be sorted by frequency in descending order, with ties broken by alphabetical dish description in ascending order.
3. THE top dishes section SHALL only include dishes for which the housemate was chef on a day when the dish was linked to the day plan.
4. EACH entry in the top dishes list SHALL display the dish description and the cooking count for that dish within the selected time range.
5. IF the housemate has no Cooking_Days in the selected range, THEN THE top dishes section SHALL not be displayed.

### Requirement 8: Housemate Timeline Chart

**User Story:** As a housemate, I want to see a visual timeline of which dishes I cooked over time, so that I can observe cooking patterns and variety.

#### Acceptance Criteria

1. THE HousemateDetailsPage SHALL display a Timeline_Chart with dishes on the vertical axis and time on the horizontal axis.
2. THE Timeline_Chart SHALL display one row per saved dish that the housemate has been chef for on at least one Cooking_Day across all time, regardless of the currently loaded timeline window.
3. THE Timeline_Chart SHALL sort dish rows by all-time frequency in descending order (most frequently cooked dishes at the top), using alphabetical dish description as tie-breaker when two dishes have equal frequency.
4. THE Timeline_Chart SHALL display a dot or small marker for each day on which the housemate was chef and the dish was linked to the day plan.
5. THE Timeline_Chart SHALL be horizontally scrollable, with the right edge representing the current month.
6. WHEN the Timeline_Chart loads, THE system SHALL pre-load 3 months of timeline data counting backwards from the current date.
7. WHEN the user scrolls the Timeline_Chart to the left beyond the loaded data boundary, THE system SHALL fetch an additional 1-month chunk of data prepended to the existing timeline (infinite scroll-back).

### Requirement 9: Soft-Deleted Dish Exclusion

**User Story:** As a housemate, I want statistics to only reflect active dishes, so that deleted dishes do not skew my analytics.

#### Acceptance Criteria

1. WHEN computing statistics for any page (DishDetailsPage or HousemateDetailsPage), THE Statistics_Engine SHALL exclude all DayPlanDishLinks that reference a SavedDish where IsDeleted equals true, such that excluded links do not contribute to times cooked counts, timeline chart dots, top dishes lists, or donut chart values.
2. WHEN a previously soft-deleted dish is reactivated (IsDeleted set back to false), THE Statistics_Engine SHALL include that dish's DayPlanDishLinks in the next statistics computation requested by the client.
3. IF a user navigates to a soft-deleted dish's route (`/saved-dishes/{id}`), THEN THE application SHALL redirect to the SavedDishesPage without displaying the DishDetailsPage content.
4. WHILE a SavedDish has IsDeleted equal to true, THE HousemateDetailsPage Timeline_Chart SHALL not display a row for that dish.

### Requirement 10: No-Data Empty State

**User Story:** As a housemate, I want to see a clear message when no cooking data exists for the selected time range, so that I understand why statistics are empty rather than seeing a broken page.

#### Acceptance Criteria

1. WHEN no Cooking_Days exist for the dish within the selected time range, THE DishDetailsPage SHALL hide the summary statistics section and display a localized empty state message indicating no cooking data is available for the selected period.
2. WHEN no chef days exist for the housemate within the selected time range, THE HousemateDetailsPage SHALL hide the summary statistics section, the donut chart, and the top dishes section, and display a localized empty state message indicating no cooking data is available for the selected period.
3. WHEN the Timeline_Chart has no data points to display within its loaded time window, THE Timeline_Chart area SHALL hide the chart axes and rows and show a localized empty state message in place of the chart.
4. WHEN the user selects a different time range that does contain Cooking_Days, THE DishDetailsPage or HousemateDetailsPage SHALL hide the empty state message and display the summary statistics normally.
5. IF the Timeline_Chart has no data points but the summary statistics section does have data for the selected time range, THEN THE system SHALL display the summary statistics normally while showing the Timeline_Chart empty state independently.

### Requirement 11: Statistics API Endpoint

**User Story:** As a developer, I want a dedicated API endpoint for statistics data, so that the frontend can request computed statistics without performing complex client-side calculations.

#### Acceptance Criteria

1. THE API SHALL expose a `GET /api/saved-dishes/{id}/statistics` endpoint that returns dish statistics for a given saved dish within the household.
2. THE API SHALL expose a `GET /api/housemates/{id}/statistics` endpoint that returns housemate cooking statistics within the household.
3. THE API SHALL accept a `from` query parameter (ISO 8601 date in `yyyy-MM-dd` format) and a `to` query parameter (ISO 8601 date in `yyyy-MM-dd` format) to define the time range for summary statistics.
4. THE API SHALL accept a `timelineFrom` query parameter (ISO 8601 date in `yyyy-MM-dd` format) and a `timelineTo` query parameter (ISO 8601 date in `yyyy-MM-dd` format) to define the data window for timeline chart data.
5. IF any date query parameter is missing or not in valid `yyyy-MM-dd` format, THEN THE API SHALL return HTTP 400 with error code `BAD_REQUEST`.
6. IF the `from` date is after the `to` date, or the `timelineFrom` date is after the `timelineTo` date, THEN THE API SHALL return HTTP 400 with error code `BAD_REQUEST`.
7. IF the requested saved dish does not exist or is soft-deleted, THEN THE API SHALL return HTTP 404 with error code `NOT_FOUND`.
8. IF the requested housemate does not exist, THEN THE API SHALL return HTTP 404 with error code `NOT_FOUND`.
9. IF the `{id}` route parameter is not a valid GUID, THEN THE API SHALL return HTTP 404 with error code `NOT_FOUND`.
10. THE API SHALL require a valid JWT (Authorization header) and X-Housemate-Id header, consistent with existing API conventions.

### Requirement 12: Feature Prompt Cleanup

**User Story:** As a developer, I want the feature prompts folder to only contain prompts for features that still need to be implemented, so that the folder stays clean and actionable.

#### Acceptance Criteria

1. WHEN all tasks in this spec have been completed, THE file `.kiro/specs/feature-prompts/prompt-statistics.md` SHALL be deleted from the repository.

### Requirement 13: Mobile-Friendly Visual Design

**User Story:** As a housemate using a phone, I want the statistics pages to be visually appealing and easy to interact with on a small screen, so that I can explore cooking data comfortably.

#### Acceptance Criteria

1. THE DishDetailsPage and HousemateDetailsPage SHALL use the same page layout, padding, font sizes, and styling conventions as the existing pages in the application.
2. THE Time_Range_Selector pills SHALL have a minimum touch target size of 44x44 CSS pixels.
3. WHEN the user performs a horizontal swipe gesture on the Timeline_Chart on a touch device, THE Timeline_Chart SHALL scroll horizontally in the direction of the swipe.
4. THE summary statistics numbers SHALL be visually prominent, using a larger font size than body text to serve as focal points, consistent with the existing typographic scale used elsewhere in the application.
5. THE donut chart SHALL be sized to fit within the viewport width without requiring horizontal scrolling.
6. THE DishDetailsPage and HousemateDetailsPage SHALL arrange all content sections (summary statistics, charts, lists) in a vertical stack consistent with the single-column mobile layout used by other pages in the application.
