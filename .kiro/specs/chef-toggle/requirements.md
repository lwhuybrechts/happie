# Requirements Document

## Introduction

The Chef Toggle feature adds a new independent toggle button to the attendance section of the day plan. The button displays a chef's hat icon and indicates which housemates will help with cooking on a given day. Unlike the three attendance radio buttons (EatingIn, Unknown, NotEatingIn), the chef toggle operates independently — a housemate can be marked as chef regardless of their attendance status, and multiple housemates can be chef simultaneously. The feature integrates with the history audit log.

## Glossary

- **Chef_Toggle**: A binary on/off button displayed to the left of the attendance radio buttons, indicating whether a housemate is cooking on a given day.
- **Chef_Status**: A boolean value per housemate per day representing whether the housemate is marked as chef (true) or not (false).
- **Auto_Chef_Assignment**: The automatic enabling of Chef_Status for the housemate who first fills in a dish on a day where no housemate has Chef_Status enabled.
- **Day_Plan**: The full plan for a specific date in a household, including attendance, dish, comments, and chef statuses.
- **Attendance_Section**: The UI component displaying housemate rows with their attendance buttons and chef toggle.
- **DayHistory**: The audit log recording all changes made to a Day_Plan.
- **System**: The Happie application (frontend and backend combined).

## Requirements

### Requirement 1: Display Chef Toggle Button

**User Story:** As a housemate, I want to see a chef's hat toggle button next to each housemate's attendance buttons, so that I can indicate who is cooking.

#### Acceptance Criteria

1. THE Attendance_Section SHALL display a Chef_Toggle button to the left of the three attendance radio buttons for each active housemate.
2. THE Chef_Toggle SHALL display a chef's hat icon.
3. WHEN Chef_Status is enabled for a housemate, THE Chef_Toggle SHALL apply a visually distinct active CSS class that differentiates it from the inactive state.
4. WHEN Chef_Status is disabled for a housemate, THE Chef_Toggle SHALL display without the active CSS class, appearing visually muted compared to the active state.
5. THE Chef_Toggle SHALL include an aria-label sourced from IStringLocalizer and an aria-pressed attribute set to "true" when Chef_Status is enabled or "false" when Chef_Status is disabled.
6. WHEN the Attendance_Section loads for a given date, THE Chef_Toggle SHALL default to the disabled state for each housemate unless a persisted Chef_Status indicates otherwise.

### Requirement 2: Toggle Chef Status Independently

**User Story:** As a housemate, I want the chef toggle to work independently from the attendance radio buttons, so that I can mark someone as chef regardless of whether they are eating in.

#### Acceptance Criteria

1. WHEN a housemate clicks the Chef_Toggle for a housemate on a given day, THE System SHALL toggle the Chef_Status between enabled and disabled for that housemate on that day.
2. THE System SHALL allow Chef_Status to be enabled regardless of the housemate's current attendance status (Unknown, EatingIn, or NotEatingIn).
3. WHEN a housemate changes their attendance status, THE System SHALL preserve the existing Chef_Status for that housemate on that day.
4. THE System SHALL default Chef_Status to disabled for any housemate on a day where no Chef_Status has been previously set.
5. THE System SHALL allow multiple housemates to have Chef_Status enabled on the same day simultaneously.
6. THE System SHALL allow any housemate in the household to toggle the Chef_Status of any other housemate for any day.

### Requirement 3: Multiple Chefs Per Day

**User Story:** As a housemate, I want multiple people to be marked as chef on the same day, so that cooking responsibilities can be shared.

#### Acceptance Criteria

1. THE System SHALL allow Chef_Status to be toggled between enabled and disabled for each active housemate on any given day, with no limit on how many housemates can have Chef_Status enabled on the same day.
2. THE System SHALL store Chef_Status independently per housemate per day, such that enabling or disabling Chef_Status for one housemate does not change the Chef_Status of any other housemate on that day.
3. THE System SHALL allow any active housemate to toggle the Chef_Status of any other active housemate for any day.

### Requirement 4: Cross-Housemate Chef Toggling

**User Story:** As a housemate, I want to toggle the chef status of any other housemate, so that anyone can update the cooking plan.

#### Acceptance Criteria

1. THE System SHALL allow any active housemate to toggle the Chef_Status of any other active housemate for any day.
2. THE System SHALL attribute the Chef_Status change to the acting housemate (identified by X-Housemate-Id) in the DayHistory.
3. IF the target housemate does not exist or is soft-deleted, THEN THE System SHALL return a not-found error and SHALL NOT create a DayHistory entry.
4. THE System SHALL use last-write-wins semantics for Chef_Status, such that concurrent toggles by different housemates result in the most recently written value being persisted.

### Requirement 5: Auto-Chef Assignment on Dish Entry

**User Story:** As a housemate, I want to be automatically marked as chef when I fill in a dish and no one else is chef yet, so that the cooking responsibility is implicitly assigned.

#### Acceptance Criteria

1. WHEN a housemate saves a non-empty dish description (after trimming, length ≥ 1 character) AND no active housemate has Chef_Status enabled for that day, THE System SHALL automatically enable Chef_Status for the acting housemate.
2. WHEN a housemate saves a non-empty dish description AND at least one active housemate already has Chef_Status enabled for that day (including the acting housemate themselves), THE System SHALL not modify any Chef_Status.
3. WHEN Auto_Chef_Assignment occurs, THE System SHALL record a DayHistory entry that includes the acting housemate's identity, the target date, and a ChangeType value distinct from manual chef toggles, so that automatic assignments are distinguishable from manual Chef_Status changes in the history log.
4. WHEN a housemate saves an empty dish description (after trimming, length = 0), THE System SHALL not modify any Chef_Status regardless of current chef assignments for that day.

### Requirement 6: Auto-Chef Persistence After Dish Cleared

**User Story:** As a housemate, I want the chef status to remain set even if the dish is cleared, so that I don't accidentally lose the cooking assignment.

#### Acceptance Criteria

1. WHEN the dish description is cleared (set to empty), THE System SHALL preserve all existing Chef_Status values for that day without modification, regardless of whether Chef_Status was set manually or via Auto_Chef_Assignment.
2. WHEN the dish description is cleared (set to empty), THE System SHALL NOT trigger Auto_Chef_Assignment or any other automatic Chef_Status change.
3. THE System SHALL only change a housemate's Chef_Status through an explicit Chef_Toggle action by a housemate or through Auto_Chef_Assignment when a dish is first entered on a day with no chef.

### Requirement 7: Chef Status in Day Plan API Response

**User Story:** As a frontend developer, I want the day plan API to include chef status per housemate, so that the UI can render the toggle correctly.

#### Acceptance Criteria

1. THE day plan API response SHALL include a boolean `isChef` field (JSON property name: "isChef") for each housemate in the attendance list.
2. WHEN no chef record exists for a housemate on a given day, THE System SHALL default the `isChef` field to false in the response.
3. THE `isChef` field SHALL be stored and returned independently per housemate per day, following the same per-housemate-per-day pattern as attendance records.

### Requirement 8: Chef Status History Tracking

**User Story:** As a housemate, I want chef status changes to appear in the day history log, so that I can see who changed the cooking plan.

#### Acceptance Criteria

1. WHEN Chef_Status is toggled (enabled or disabled) by a housemate, THE System SHALL create a DayHistory entry with ChangeType `ChefStatusChanged`, the acting housemate's ID as ChangedByHousemateId, and a description containing the target housemate's name and the new Chef_Status value (enabled or disabled).
2. THE DayHistory entry SHALL be associated with the date for which the Chef_Status was changed, using the same PartitionKey and RowKey format as existing DayHistory entries.
3. WHEN Auto_Chef_Assignment occurs, THE System SHALL create a DayHistory entry with ChangeType `ChefStatusChanged`, ChangedByHousemateId set to the acting housemate's ID, and a description indicating the assignment was automatic.
4. IF the target housemate does not exist in the household, THEN THE System SHALL not create a DayHistory entry and SHALL return a not-found error.

### Requirement 9: Optimistic UI for Chef Toggle

**User Story:** As a housemate, I want the chef toggle to respond instantly when clicked, so that the app feels fast and responsive.

#### Acceptance Criteria

1. WHEN a housemate clicks the Chef_Toggle, THE System SHALL update the visual state to reflect the new Chef_Status within the same render cycle, before awaiting the API response.
2. IF the API call to save Chef_Status fails, THEN THE System SHALL revert the Chef_Toggle to its previous visual state.
3. IF the API call to save Chef_Status fails, THEN THE System SHALL display a localized error toast notification using the existing toast system.
4. WHILE an API call to save Chef_Status is in flight for a housemate, THE System SHALL ignore additional clicks on that housemate's Chef_Toggle.

### Requirement 10: Chef Toggle Localization

**User Story:** As a housemate, I want the chef toggle labels and history descriptions to be available in both Dutch and English, so that the feature works with the existing language support.

#### Acceptance Criteria

1. THE System SHALL provide a localized Chef_Toggle aria-label string in both Dutch and English resource files, using a format placeholder for the housemate name (e.g., "Chef status for {0}").
2. THE System SHALL provide localized DayHistory description strings in both Dutch and English resource files for each distinct chef change type: manual chef toggle (enabled/disabled) and Auto_Chef_Assignment.
3. THE System SHALL retrieve all chef-toggle-related user-visible strings via IStringLocalizer<AppStrings> and SHALL NOT hardcode any English or Dutch text directly in .razor components.
