# Requirements Document

## Introduction

The dinner-time feature allows housemates to set an optional dinner time (hour and minute) for a given day. The time is displayed in the dish section of the DayPlanPage and can be edited inline alongside the dish description. When the dinner time is set or changed, other housemates receive a push notification if the configured time is less than 6 hours away. The dinner time is a naive, timezone-agnostic value representing "what time dinner is at the house" — it is stored and displayed as-is with no timezone conversion anywhere in the system. The 6-hour notification window is evaluated using the setter's local time, derived from the server's UTC clock adjusted by a client-provided timezone offset.

## Glossary

- **DishPanel**: The Blazor component responsible for displaying and editing the planned dish for a day, including the new dinner time field.
- **Dinner_Time**: An optional hour-and-minute value representing when dinner will be served on a given day.
- **DishRecord**: The Azure Table Storage entity storing the dish description and dinner time for a specific day in a household. PartitionKey = `{HouseholdId}`, RowKey = `{YYYY-MM-DD}`.
- **Time_Picker**: A native platform time input (iOS scroll wheels) with a browser-compatible fallback (`<input type="time">`).
- **Notification_Window**: The 6-hour window before a configured dinner time during which a push notification is triggered upon changes to the dinner time. The window is evaluated by comparing the setter's current local time (server UTC clock + client-provided timezone offset) against the naive dinner time.
- **Timezone_Offset**: An integer representing the setter's timezone offset in minutes ahead of UTC (e.g., UTC+2 = 120, UTC-5 = -300). Provided by the client on every dish save request via `DateTimeOffset.Now.Offset.TotalMinutes` in Blazor WASM. Not stored — used transiently for the notification window calculation only.

## Requirements

### Requirement 1: Display Dinner Time in Read Mode

**User Story:** As a housemate, I want to see the configured dinner time in the dish section, so that I know when dinner will be served.

#### Acceptance Criteria

1. WHEN a Dinner_Time value is stored for the viewed day's DishRecord, THE DishPanel SHALL display the time formatted as `HH:mm` on the right side of the dish section body, vertically aligned with the dish description text.
2. WHEN no Dinner_Time value is stored for the viewed day's DishRecord, THE DishPanel SHALL hide the dinner time display entirely (no empty placeholder or reserved space).
3. THE DishPanel SHALL render the dinner time text with font-size 20px, font-weight 700, and color #ffffff (matching the dish description text style).
4. THE DishPanel SHALL render a localized label reading "Dinner time" (via `IStringLocalizer<AppStrings>`) below the time value, aligned to the right, with font-size 12px and color #718096 (matching the "last edited" metadata text style).
5. WHILE Dinner_Time is displayed, THE DishPanel SHALL reserve a maximum of 30% of the dish section body width for the dinner time column so that the dish text and dinner time do not overlap.
6. IF the dish description text is too long to fit in the remaining width beside the dinner time column, THEN THE DishPanel SHALL truncate the dish text with word-break rather than overlapping or pushing the dinner time off-screen.

### Requirement 2: Edit Dinner Time

**User Story:** As a housemate, I want to set or clear the dinner time while editing the dish, so that I can inform everyone when dinner will be served.

#### Acceptance Criteria

1. WHEN the DishPanel enters edit mode, THE DishPanel SHALL display a time input below the dish text input, with a localized header label reading "Dinner time".
2. THE DishPanel SHALL style the dinner time input consistently with the dish text input (same border, padding, font size).
3. THE DishPanel SHALL indicate that setting the dinner time is optional (via a localized placeholder or helper text).
4. WHEN a Dinner_Time value is already stored, THE DishPanel SHALL pre-populate the time input with the existing value formatted as `HH:mm`.
5. WHEN a Dinner_Time value is already stored, THE DishPanel SHALL display a clear button adjacent to the time input that removes the dinner time value from the input.
6. WHEN the user has entered a new Dinner_Time value in the time input, THE DishPanel SHALL display a clear button adjacent to the time input that removes the dinner time value from the input.
7. THE DishPanel SHALL accept hour values from 0 to 23 and minute values from 0 to 59 in `HH:mm` format.
8. IF the time input contains a value that is not a valid `HH:mm` time (hour outside 0–23 or minute outside 0–59), THEN THE DishPanel SHALL disable the save button and display a localized validation message below the time input.
9. WHEN the user exits edit mode without saving, THE DishPanel SHALL discard any unsaved dinner time changes and revert the time input state to the last stored value.

### Requirement 3: Edit Mode Animation

**User Story:** As a housemate, I want the dish section to smoothly expand and collapse when entering and exiting edit mode, so that the additional dinner time input does not appear jarring.

#### Acceptance Criteria

1. WHEN the DishPanel transitions from read mode to edit mode, THE DishPanel SHALL animate the vertical height increase using a CSS transition with ease timing over 300 milliseconds.
2. WHEN the DishPanel transitions from edit mode to read mode (either by saving or discarding), THE DishPanel SHALL animate the vertical height decrease using a CSS transition with ease timing over 300 milliseconds.
3. THE DishPanel SHALL keep content clipped to the panel bounds during the height animation so that overflowing content is not visible outside the animating area.
4. IF the user has enabled `prefers-reduced-motion: reduce` in their operating system settings, THEN THE DishPanel SHALL skip the height animation and apply the height change immediately.

### Requirement 4: Time Picker Input

**User Story:** As a housemate using an iOS device, I want the time input to use the native iOS scroll-wheel picker, so that I can quickly select a time.

#### Acceptance Criteria

1. THE DishPanel SHALL render the time input as an HTML `<input type="time">` element with a step value of 60 seconds (1-minute granularity, no seconds selector) so that iOS and Android browsers present their native time picker interface.
2. WHILE running in a browser that does not support `<input type="time">` natively, THE DishPanel SHALL fall back to a text input that accepts `HH:mm` format with a localized placeholder showing the expected format pattern (resolved via `IStringLocalizer<AppStrings>`).
3. THE DishPanel SHALL associate the time input with a visible localized label so that screen readers announce the field purpose.

### Requirement 5: Persist Dinner Time

**User Story:** As a housemate, I want the dinner time to be saved alongside the dish, so that it persists across sessions and is visible to all housemates.

#### Acceptance Criteria

1. WHEN the user saves the dish with a Dinner_Time value, THE DishPanel SHALL include the dinner time hour (0–23) and minute (0–59) fields in the API request to the server.
2. WHEN the user saves the dish with a cleared Dinner_Time value, THE DishPanel SHALL send null for both the hour and minute fields to the server, removing the dinner time from the DishRecord.
3. THE API SHALL store the Dinner_Time as optional hour and minute fields on the existing DishRecord entity (same table, same PartitionKey/RowKey pattern).
4. IF the dinner time hour and minute are both provided, THEN THE API SHALL validate that the hour is between 0 and 23 inclusive and the minute is between 0 and 59 inclusive.
5. IF only one of the dinner time fields (hour or minute) is provided while the other is null, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`, indicating that both fields must be provided together or both must be null.
6. IF the dinner time values fail validation, THEN THE API SHALL return HTTP 422 with error code `VALIDATION_ERROR`.
7. WHEN the DishRecord has dinner time values set, THE API SHALL return the hour and minute fields in the DishDto response.
8. WHEN the DishRecord has no dinner time values set, THE API SHALL return null for the hour and minute fields in the DishDto response.
9. THE DishPanel SHALL enforce the same hour (0–23) and minute (0–59) validation on the client side before sending the request to the server.
10. THE DishPanel SHALL include the client's timezone offset (in minutes ahead of UTC, obtained via `(int)DateTimeOffset.Now.Offset.TotalMinutes`) as a required `timezoneOffsetMinutes` field in every `UpdateDishRequest`. This field is always required and is used by the server for the notification window calculation only — it is not stored.

### Requirement 6: Push Notification on Dinner Time Change

**User Story:** As a housemate, I want to be notified when the dinner time is set or changed and dinner is approaching, so that I can plan accordingly.

#### Acceptance Criteria

1. WHEN a housemate saves a Dinner_Time value that differs from the previously stored value (including setting it for the first time), AND the setter's current local time (derived from the server's UTC clock adjusted by the client-provided timezone offset) is less than 6 hours before the new Dinner_Time on that day, THE API SHALL send a push notification to all other active housemates in the household who have a registered push subscription.
2. WHEN a housemate saves a Dinner_Time value that differs from the previously stored value, AND the setter's current local time (derived from the server's UTC clock adjusted by the client-provided timezone offset) is 6 or more hours before the new Dinner_Time on that day, THE API SHALL not send a push notification for the dinner time change.
3. WHEN a housemate saves a Dinner_Time value identical to the previously stored value, THE API SHALL not send a push notification for the dinner time change.
4. WHEN a housemate clears a previously set Dinner_Time value, THE API SHALL not send a push notification for the dinner time change.
5. THE push notification payload SHALL include the name of the housemate who made the change, the affected date, and the new time value formatted as `HH:mm`.
6. WHEN both the dish description AND the dinner time change in the same save operation, THE API SHALL send exactly ONE push notification with a combined translation key that includes both the dish description and the dinner time information, rather than sending two separate push notifications.
7. IF push delivery fails for one recipient, THEN THE API SHALL continue delivering to remaining recipients without interruption.
8. IF push delivery fails, THEN THE API SHALL log the failure server-side without rolling back the Dinner_Time save operation.

### Requirement 7: Localization

**User Story:** As a housemate, I want all dinner time labels and messages to be displayed in my configured language, so that the feature is consistent with the rest of the app.

#### Acceptance Criteria

1. THE DishPanel SHALL display the "Dinner time" label in both read mode and edit mode using a resource key resolved via `IStringLocalizer<AppStrings>` with translations defined in both `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English).
2. THE DishPanel SHALL display placeholder text for the time input (visible when no value is entered) using a localized resource key resolved via `IStringLocalizer<AppStrings>`, indicating that the field is optional.
3. WHEN the API sends a dinner-time-change push notification, THE API SHALL resolve the notification body text per recipient using `SharedStringResolver` with the recipient's stored locale from their push subscription record.
4. THE DishPanel SHALL display the time input validation error message using a localized resource key resolved via `IStringLocalizer<AppStrings>`.

### Requirement 8: Day History Entry on Dinner Time Change

**User Story:** As a housemate, I want dinner time changes to appear in the day history log, so that I can see who changed the dinner time and when.

#### Acceptance Criteria

1. WHEN a housemate saves a Dinner_Time value that differs from the previously stored value (including setting it for the first time when no value exists), THE API SHALL create a DayHistoryEntry with ChangeType `DinnerTime`, the acting housemate's ID as `ChangedByHousemateId`, and a translation key `history_dinner_time_set` with a `time` parameter containing the new time formatted as `HH:mm`.
2. WHEN a housemate saves a null or empty Dinner_Time value while a non-null value is currently stored, THE API SHALL create a DayHistoryEntry with ChangeType `DinnerTime`, the acting housemate's ID as `ChangedByHousemateId`, and a translation key `history_dinner_time_cleared` with no parameters.
3. WHEN a housemate saves a Dinner_Time value identical to the previously stored value (including saving null or empty when the stored value is already null or empty), THE API SHALL not create a DayHistoryEntry.
4. THE API SHALL store the DayHistoryEntry using PartitionKey `{HouseholdId}` and RowKey `{YYYY-MM-DD}_{InvertedTimestamp}` so that entries appear in reverse-chronological order in the DayHistoryLog.
5. THE DayHistoryLog SHALL display dinner time history entries using localized text resolved via `SharedStringResolver` with keys `history_dinner_time_set` and `history_dinner_time_cleared` defined in both `SharedStrings.resx` (Dutch) and `SharedStrings.en.resx` (English).
6. WHEN a housemate saves both a changed dish description and a changed Dinner_Time value in the same request, THE API SHALL create a single combined DayHistoryEntry with ChangeType `DishAndDinnerTime`, a translation key `history_dish_and_dinner_time_set` with parameters containing both the new dish description and the new time formatted as `HH:mm`, rather than creating two separate history entries.
7. WHEN a housemate saves both a changed dish description and a cleared Dinner_Time value in the same request, THE API SHALL create a single combined DayHistoryEntry with ChangeType `DishAndDinnerTime`, a translation key `history_dish_set_dinner_time_cleared` with a parameter containing the new dish description, rather than creating two separate history entries.
8. IF the DayHistoryEntry write fails after a successful Dinner_Time save, THEN THE API SHALL log the failure server-side but SHALL NOT roll back the Dinner_Time change or return an error to the client.
