# Implementation Plan: Dinner Time

## Overview

Add an optional dinner time (hour + minute) to each day's dish record. The implementation extends the existing `DishRecord` entity, mapper, repository, handler, API endpoint, and `DishPanel` component. The dinner time is a naive, timezone-agnostic value stored as two int fields on the entity, represented as `TimeOnly?` in the domain/handler layer, and sent as two nullable ints on the wire. Push notifications are sent when the dinner time changes within a 6-hour window (evaluated using a client-provided timezone offset). Combined history entries and consolidated push notifications are sent when both dish and dinner time change in the same save.

## Tasks

- [x] 1. Extend shared contracts and domain types
  - [x] 1.1 Add dinner time fields to `UpdateDishRequest` and `DishDto` in `Happie.Shared/Contracts/`
    - Add `int? DinnerTimeHour` and `int? DinnerTimeMinute` properties to `UpdateDishRequest`
    - Add `int TimezoneOffsetMinutes` required property to `UpdateDishRequest`
    - Add `int? DinnerTimeHour` and `int? DinnerTimeMinute` properties to `DishDto`
    - Use `[JsonPropertyName]` attributes for lowercase wire format
    - _Requirements: 5.1, 5.2, 5.7, 5.8, 5.10_

  - [x] 1.2 Add new `ChangeType` enum values and translation key constants to `Happie.Shared/Domain/`
    - Add `DinnerTime` and `DishAndDinnerTime` values to the `ChangeType` enum
    - Add translation key constants: `HistoryDinnerTimeSet`, `HistoryDinnerTimeCleared`, `HistoryDishAndDinnerTimeSet`, `HistoryDishSetDinnerTimeCleared`, `NotificationDinnerTimeChanged`, `NotificationDishAndDinnerTimeChanged`
    - _Requirements: 8.1, 8.2, 8.6, 8.7, 6.5, 6.6_

  - [x] 1.3 Add `DinnerTime` property to `DishRecord` domain type in `Happie.Api/Domain/`
    - Add `TimeOnly? DinnerTime` to the `DishRecord` record
    - _Requirements: 5.3_

- [x] 2. Extend storage layer (entity + mapper)
  - [x] 2.1 Add `DinnerTimeHour` and `DinnerTimeMinute` properties to `DishRecordEntity`
    - Add `public int DinnerTimeHour { get; set; } = -1;` (sentinel for null)
    - Add `public int DinnerTimeMinute { get; set; } = -1;` (sentinel for null)
    - _Requirements: 5.3_

  - [x] 2.2 Update `DishRecordMapper` to convert between `TimeOnly?` and int sentinel values
    - In `ToModel`: map `-1` sentinel to `null`, valid ints to `new TimeOnly(hour, minute)`
    - In `ToEntity`: map `TimeOnly?` to ints or `-1` sentinel
    - _Requirements: 5.3, 5.7, 5.8_

  - [x] 2.3 Write property test for DishRecord mapper round-trip
    - **Property 2: DishRecord mapper round-trip**
    - **Validates: Requirements 5.3, 5.7, 5.8**

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Extend handler with change detection, history, and notification logic
  - [x] 4.1 Update `IDayHandler.UpsertDishAsync` signature to accept `TimeOnly? dinnerTime` and `int timezoneOffsetMinutes`
    - Update the interface and implementation method signature
    - _Requirements: 5.1, 5.2, 6.1_

  - [x] 4.2 Implement change detection and combined history entry logic in `DayHandler.UpsertDishAsync`
    - Fetch existing `DishRecord` to compare old values
    - Determine what changed: dish only, dinner time only, both, or neither
    - Write the appropriate `DayHistoryEntry` based on what changed (single entry per save)
    - Use `DinnerTime` / `DishAndDinnerTime` change types and corresponding translation keys
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8_

  - [x] 4.3 Implement consolidated push notification logic with 6-hour window evaluation
    - Compute setter's local time: `DateTimeOffset.UtcNow.AddMinutes(timezoneOffsetMinutes)`
    - Compare against naive dinner time on the given date to evaluate the 6-hour window
    - Choose combined translation key based on what changed (dish only, dinner time only, both)
    - Call `SendAutoNotificationsAsync` at most once per save with appropriate parameters
    - Do not send push when dinner time is cleared or unchanged
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8_

  - [x] 4.4 Write property test for notification window decision
    - **Property 3: Notification window decision**
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.4**

  - [x] 4.5 Write property test for history entry change detection
    - **Property 4: History entry change detection**
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.6, 8.7**

- [x] 5. Extend API function with validation and conversion
  - [x] 5.1 Update `DaysFunction.PutDishAsync` to validate dinner time fields and convert to `TimeOnly?`
    - Validate both-or-neither constraint on `DinnerTimeHour` / `DinnerTimeMinute`
    - Validate hour ∈ [0, 23] and minute ∈ [0, 59] when provided
    - Return HTTP 422 `VALIDATION_ERROR` on failure
    - Convert validated ints to `TimeOnly?` before calling handler
    - Pass `TimezoneOffsetMinutes` through to handler
    - _Requirements: 5.4, 5.5, 5.6_

  - [x] 5.2 Update `DaysFunction.GetDayPlanAsync` (or equivalent) to include dinner time in `DishDto` response
    - Map `TimeOnly?` from domain to `int?` fields in `DishDto`
    - Return null for both fields when dinner time is not set
    - _Requirements: 5.7, 5.8_

  - [x] 5.3 Write property test for dinner time validation correctness
    - **Property 1: Dinner time validation correctness**
    - **Validates: Requirements 2.7, 5.4, 5.5, 5.9**

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Add localization resources
  - [x] 7.1 Add dinner time resource keys to `SharedStrings.resx` (Dutch) and `SharedStrings.en.resx` (English)
    - Add `history_dinner_time_set`, `history_dinner_time_cleared`, `history_dish_and_dinner_time_set`, `history_dish_set_dinner_time_cleared`
    - Add `notification_dinner_time_changed`, `notification_dish_and_dinner_time_changed`
    - _Requirements: 7.3, 8.5_

  - [x] 7.2 Add dinner time UI resource keys to `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English)
    - Add "Dinner time" label key for read and edit mode
    - Add placeholder text key indicating the field is optional
    - Add validation error message key for invalid time input
    - _Requirements: 7.1, 7.2, 7.4_

- [x] 8. Update `DishPanel` component — read mode
  - [x] 8.1 Display dinner time in read mode on the right side of the dish section body
    - Add two-column flex layout when dinner time is set (time column max 30% width)
    - Display time formatted as `HH:mm` with font-size 20px, font-weight 700, color #ffffff
    - Display localized "Dinner time" label below with font-size 12px, color #718096, right-aligned
    - Hide dinner time display entirely when no value is stored (no placeholder or reserved space)
    - Truncate dish text with word-break when it overflows the remaining width
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 9. Update `DishPanel` component — edit mode
  - [x] 9.1 Add time input with validation and clear button in edit mode
    - Render `<input type="time" step="60">` below the dish text input with localized header label
    - Style consistently with the dish text input (same border, padding, font size)
    - Show localized placeholder indicating the field is optional
    - Pre-populate with existing value formatted as `HH:mm` when stored
    - Add clear button adjacent to the input when a value is present
    - Validate hour (0–23) and minute (0–59), disable save on invalid input with localized error message
    - Discard unsaved changes and revert on cancel
    - Associate input with a visible localized label for screen readers
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 4.1, 4.2, 4.3_

  - [x] 9.2 Include dinner time and timezone offset in the save request
    - Parse `HH:mm` input to hour and minute ints for `UpdateDishRequest`
    - Send null for both fields when dinner time is cleared
    - Include `(int)DateTimeOffset.Now.Offset.TotalMinutes` as `timezoneOffsetMinutes`
    - Enforce client-side validation before sending
    - _Requirements: 5.1, 5.2, 5.9, 5.10_

- [x] 10. Implement edit mode height animation
  - [x] 10.1 Add CSS transitions for smooth expand/collapse of `DishPanel` on edit mode toggle
    - Use `max-height` with `transition: max-height 300ms ease` and `overflow: hidden`
    - Skip animation when `prefers-reduced-motion: reduce` is enabled (set `transition: none`)
    - Keep content clipped to panel bounds during animation
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The dinner time is naive and timezone-agnostic — no timezone conversion on storage or display
- The `timezoneOffsetMinutes` is used transiently for the 6-hour notification window only
- At most one push notification and one history entry per save operation, regardless of how many fields changed
- The `-1` sentinel in the entity layer is required because Azure Table Storage cannot store nullable ints

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["2.3", "4.1"] },
    { "id": 3, "tasks": ["4.2", "4.3"] },
    { "id": 4, "tasks": ["4.4", "4.5", "5.1", "5.2"] },
    { "id": 5, "tasks": ["5.3", "7.1", "7.2"] },
    { "id": 6, "tasks": ["8.1", "9.1"] },
    { "id": 7, "tasks": ["9.2", "10.1"] }
  ]
}
```
