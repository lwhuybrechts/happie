# Requirements Document

## Introduction

The attendance section currently displays all three status buttons (EatingIn ✓, Unknown ?, NotEatingIn ✗) plus the chef toggle button for each housemate row at all times. This feature collapses the attendance buttons so only the currently active button is visible by default on mobile viewports. Tapping the active button expands the row with a slide animation, revealing the other two buttons. Selecting a new status or losing focus collapses the row back to the single active button.

The collapse/expand behavior is mobile-focused: on narrow viewports (below the Mobile_Breakpoint of 480px), the full slide-in interaction applies. On wider viewports, there is sufficient horizontal space to always display all three attendance buttons in their standard expanded layout without collapse behavior. Additionally, on devices with a pointer (mouse), hovering over the active button expands the row and moving the pointer away collapses it, providing an alternative to the tap interaction.

## Glossary

- **Attendance_Row**: A single housemate's row in the attendance section, containing the avatar, name, chef button, and attendance status buttons.
- **Active_Button**: The attendance status button that represents the housemate's current attendance status (EatingIn, Unknown, or NotEatingIn).
- **Inactive_Buttons**: The two attendance status buttons that do not represent the housemate's current status.
- **Collapsed_State**: The default visual state of an Attendance_Row where only the Active_Button is visible and the Chef_Button is positioned to the left, adjacent to the Active_Button.
- **Expanded_State**: The visual state of an Attendance_Row where all three attendance buttons are visible in their standard positions.
- **Chef_Button**: The chef toggle button in an Attendance_Row.
- **Slide_Animation**: A CSS transition that moves elements horizontally with a smooth easing curve, completing within 250 milliseconds.
- **Attendance_Section**: The component responsible for rendering all Attendance_Rows.
- **Housemate_Name**: The displayed name of a housemate in an Attendance_Row.
- **Auto_Collapse_Timeout**: A 3-second timer that starts when an Attendance_Row enters Expanded_State and triggers a collapse if no attendance button is clicked before it expires.
- **Mobile_Breakpoint**: A `max-width: 480px` CSS media query threshold derived from ensuring at least 140px of available horizontal space for the Housemate_Name when all buttons are visible. Below this breakpoint, the viewport is considered narrow and the collapse/expand behavior applies. Above this breakpoint, all three attendance buttons are always visible without collapsing.
- **Pointer_Device**: An input device capable of hover interactions (e.g., a mouse or trackpad), detected via the CSS `@media (hover: hover)` media query or equivalent JavaScript check.

## Requirements

### Requirement 1: Collapsed State by Default

**User Story:** As a housemate, I want only the active attendance button to be visible by default, so that the interface is less cluttered and easier to scan.

#### Acceptance Criteria

1. WHEN the Attendance_Section renders, THE Attendance_Row SHALL display in Collapsed_State, showing only the single attendance button whose status matches the housemate's current AttendanceStatus (the Active_Button) and hiding the two buttons whose statuses do not match (the Inactive_Buttons).
2. WHILE in Collapsed_State, THE Attendance_Row SHALL hide the two Inactive_Buttons so that they are not visible and do not occupy layout space.
3. WHILE in Collapsed_State, THE Attendance_Row SHALL display the Chef_Button immediately to the left of the Active_Button with no gap larger than the standard 8px button spacing.
4. IF the housemate's current AttendanceStatus is Unknown, THEN THE Attendance_Row SHALL treat the Unknown button (?) as the Active_Button and hide the EatingIn (✓) and NotEatingIn (✗) buttons.

### Requirement 2: Expand on Active Button Tap

**User Story:** As a housemate, I want to tap the active attendance button to reveal all three status options, so that I can change my attendance status.

#### Acceptance Criteria

1. WHEN the Active_Button is clicked in Collapsed_State, THE Attendance_Row SHALL transition to Expanded_State.
2. WHILE in Collapsed_State, THE Attendance_Row SHALL position all three attendance buttons at the Active_Button's location, with the Active_Button rendered at the highest z-index so the Inactive_Buttons are hidden behind it.
3. WHEN transitioning to Expanded_State, THE Attendance_Row SHALL animate all three attendance buttons from the Active_Button's position to their final positions in the standard order (EatingIn, Unknown, NotEatingIn) using a Slide_Animation with a duration of 250 milliseconds, keeping the Active_Button at the highest z-index so the Inactive_Buttons appear to slide out from behind it.
4. WHEN transitioning to Expanded_State, THE Chef_Button SHALL slide further left from its Collapsed_State position (to the left of the Active_Button) to its Expanded_State position (to the left of the full attendance button group) using a Slide_Animation with a duration of 250 milliseconds, making room for the two additional buttons appearing.
5. WHILE in Expanded_State, THE Attendance_Row SHALL display all three attendance status buttons in their standard order (EatingIn, Unknown, NotEatingIn) with the Chef_Button positioned to the left of the attendance buttons.
6. WHILE the Slide_Animation is in progress, THE Attendance_Row SHALL ignore additional tap events on the Active_Button.

### Requirement 3: Collapse on Status Selection

**User Story:** As a housemate, I want the buttons to collapse after I select a new status, so that the interface returns to its compact state.

#### Acceptance Criteria

1. WHEN a different attendance status button is clicked in Expanded_State, THE Attendance_Row SHALL transition to Collapsed_State using a Slide_Animation with a duration of 250 milliseconds.
2. WHEN collapsing after a status change, THE Attendance_Row SHALL display the newly selected button as the Active_Button.
3. WHEN collapsing after a status change, THE Inactive_Buttons SHALL slide behind the new Active_Button using a Slide_Animation with a duration of 250 milliseconds.
4. WHEN collapsing after a status change, THE Chef_Button SHALL slide right toward the Active_Button to its Collapsed_State position (adjacent to the left of the Active_Button) using a Slide_Animation with a duration of 250 milliseconds.
5. IF the attendance status API call fails after collapsing, THEN THE Attendance_Row SHALL revert the Active_Button to the previous status and transition back to Collapsed_State displaying the original Active_Button.

### Requirement 4: Collapse on Focus Loss

**User Story:** As a housemate, I want the buttons to collapse when I tap elsewhere on the page, so that expanded rows do not remain open unnecessarily.

#### Acceptance Criteria

1. WHEN a click occurs outside the Expanded_State Attendance_Row, THE Attendance_Row SHALL transition to Collapsed_State using a Slide_Animation with a duration of 250 milliseconds.
2. WHEN collapsing on focus loss, THE Attendance_Row SHALL retain the current Active_Button as the visible button.
3. WHEN collapsing on focus loss, THE Inactive_Buttons SHALL slide behind the Active_Button using a Slide_Animation with a duration of 250 milliseconds.
4. WHEN collapsing on focus loss, THE Chef_Button SHALL slide right toward the Active_Button to its Collapsed_State position (adjacent to the left of the Active_Button) using a Slide_Animation with a duration of 250 milliseconds.

### Requirement 5: Collapse on Same Button Re-tap

**User Story:** As a housemate, I want to tap the active button again to collapse the row without changing my status.

#### Acceptance Criteria

1. WHILE the Attendance_Row is in Expanded_State, WHEN the user clicks the attendance status button that matches the housemate's current Attendance_Status, THE Attendance_Row SHALL transition to Collapsed_State using a Slide_Animation with a duration of 250 milliseconds.
2. WHILE the Attendance_Row is in Expanded_State, WHEN the user clicks the attendance status button that matches the housemate's current Attendance_Status, THE System SHALL retain the housemate's existing Attendance_Status without sending an update request to the backend.
3. WHILE the Slide_Animation is in progress, THE Attendance_Row SHALL not respond to additional button clicks until the transition to Collapsed_State is complete.

### Requirement 6: Single Row Expansion

**User Story:** As a housemate, I want only one attendance row to be expanded at a time, so that the interface remains tidy.

#### Acceptance Criteria

1. WHEN the Attendance_Section is first rendered, THE Attendance_Section SHALL display all Attendance_Rows in Collapsed_State.
2. WHEN an Attendance_Row transitions to Expanded_State, THE Attendance_Section SHALL collapse any other Attendance_Row that is currently in Expanded_State.
3. WHEN another Attendance_Row is collapsed due to a new row expanding, THE previously expanded row SHALL transition to Collapsed_State using a Slide_Animation with a duration of 250 milliseconds.

### Requirement 7: Animation Characteristics

**User Story:** As a housemate, I want the slide animations to feel smooth and responsive, so that the interaction is pleasant to use.

#### Acceptance Criteria

1. THE Slide_Animation SHALL complete within 250 milliseconds.
2. WHEN an Attendance_Row is expanding, THE Slide_Animation SHALL use an ease-out timing function.
3. WHEN an Attendance_Row is collapsing, THE Slide_Animation SHALL use an ease-in timing function.
4. WHILE a Slide_Animation is in progress, THE Attendance_Row SHALL ignore additional expand or collapse interactions until the current animation completes.

### Requirement 8: Accessibility

**User Story:** As a housemate using assistive technology, I want the collapsed and expanded states to be communicated properly, so that I can understand and interact with the attendance buttons.

#### Acceptance Criteria

1. WHILE in Collapsed_State, THE Inactive_Buttons SHALL have `aria-hidden="true"` and `tabindex="-1"` to prevent screen readers from announcing and keyboard focus from reaching hidden buttons.
2. WHEN transitioning to Expanded_State, THE Inactive_Buttons SHALL have `aria-hidden` and `tabindex="-1"` removed so screen readers can announce and keyboard users can reach all options.
3. WHILE in Collapsed_State, THE Active_Button SHALL have `aria-expanded="false"`.
4. WHILE in Expanded_State, THE Active_Button SHALL have `aria-expanded="true"`.

### Requirement 9: Name Shortening on Expand

**User Story:** As a housemate, I want long housemate names to be shortened when the buttons expand, so that the row layout does not overflow or break.

#### Acceptance Criteria

1. WHEN an Attendance_Row transitions to Expanded_State and the Housemate_Name is too long to fit the remaining horizontal space, THE Attendance_Row SHALL truncate the Housemate_Name with an ellipsis to prevent the row from overflowing.
2. WHEN an Attendance_Row transitions back to Collapsed_State, THE Attendance_Row SHALL restore the Housemate_Name to its previous display.
3. THE Attendance_Row SHALL determine available space for the Housemate_Name dynamically based on the current row width minus the width of all visible buttons and standard spacing.

### Requirement 10: Auto-Collapse Timeout

**User Story:** As a housemate, I want an expanded row to collapse automatically after a period of inactivity, so that the interface does not remain in an expanded state indefinitely.

#### Acceptance Criteria

1. WHEN an Attendance_Row transitions to Expanded_State, THE Attendance_Row SHALL start a 3-second Auto_Collapse_Timeout.
2. IF the Auto_Collapse_Timeout expires without an attendance button being clicked, THEN THE Attendance_Row SHALL transition to Collapsed_State using a Slide_Animation with a duration of 250 milliseconds.
3. WHEN an attendance button is clicked while the Attendance_Row is in Expanded_State, THE Attendance_Row SHALL cancel the Auto_Collapse_Timeout.
4. WHEN an Attendance_Row transitions to Collapsed_State for any reason, THE Attendance_Row SHALL cancel any active Auto_Collapse_Timeout.

### Requirement 11: Responsive Breakpoint — Always Expanded on Wide Viewports

**User Story:** As a housemate using a tablet or desktop, I want all three attendance buttons to always be visible without needing to tap, so that I can change my status directly.

#### Acceptance Criteria

1. WHILE the viewport width is above the Mobile_Breakpoint (480px), THE Attendance_Row SHALL display all three attendance buttons in their standard positions (Expanded_State layout) without Collapsed_State behavior.
2. WHILE the viewport width is above the Mobile_Breakpoint (480px), THE Attendance_Row SHALL not apply the collapse/expand interaction, the Slide_Animation, or the Auto_Collapse_Timeout.
3. WHILE the viewport width is above the Mobile_Breakpoint (480px), THE Attendance_Row SHALL position the Chef_Button to the left of the attendance buttons.
4. WHEN the viewport width crosses below the Mobile_Breakpoint (480px), THE Attendance_Row SHALL transition to Collapsed_State displaying only the Active_Button.
5. WHEN the viewport width crosses above the Mobile_Breakpoint (480px), THE Attendance_Row SHALL transition to the always-expanded layout displaying all three buttons.

### Requirement 12: Hover Interaction on Pointer Devices

**User Story:** As a housemate using a mouse, I want to hover over the active button to expand the row, so that I can see all options without clicking.

#### Acceptance Criteria

1. WHILE a Pointer_Device is detected and the viewport width is below the Mobile_Breakpoint, WHEN the pointer hovers over the Active_Button in Collapsed_State, THE Attendance_Row SHALL transition to Expanded_State using a Slide_Animation with a duration of 250 milliseconds.
2. WHILE a Pointer_Device is detected and the Attendance_Row is in Expanded_State due to a hover interaction, WHEN the pointer leaves the Attendance_Row boundary, THE Attendance_Row SHALL transition to Collapsed_State using a Slide_Animation with a duration of 250 milliseconds.
3. WHILE a Pointer_Device is detected and the Attendance_Row is in Expanded_State due to a hover interaction, THE Auto_Collapse_Timeout SHALL not start until the pointer leaves the Attendance_Row boundary.
4. WHILE a Pointer_Device is detected and the Attendance_Row is in Expanded_State due to hover, WHEN the user clicks an attendance button, THE Attendance_Row SHALL process the click as a status change (per Requirement 3) and collapse regardless of pointer position.
