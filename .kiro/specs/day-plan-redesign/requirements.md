# Requirements Document

## Introduction

This document specifies the redesign of the Day Plan page and the surrounding application layout (MainLayout, NavMenu) for the Happie PWA. The redesign introduces a responsive layout with a desktop sidebar and mobile bottom navigation, restructures the navigation menu, and visually overhauls the Day Plan page content sections (date navigation, dish, attendance, comments, history, and nudge).

## Visual References

The following mockup images provide the visual design targets for this redesign:

- [mockup-day-plan.png](./mockup-day-plan.png) — Desktop layout with sidebar and day plan content
- [mockup-day-plan-mobile.png](./mockup-day-plan-mobile.png) — Mobile layout with header and bottom navigation
- [mockup-comments.png](./mockup-comments.png) — Comments section display state
- [mockup-edit-comments.png](./mockup-edit-comments.png) — Comments section editing state
- [mockup-edit-dish.png](./mockup-edit-dish.png) — Dish panel editing state
- [mockup-nudge-modal.png](./mockup-nudge-modal.png) — Nudge modal overlay

## Glossary

- **App**: The Happie Blazor WebAssembly PWA.
- **Sidebar**: The fixed left-side navigation panel visible on desktop viewports (≥641px).
- **Bottom_Navigation_Bar**: A floating navigation bar at the bottom of the screen on mobile viewports (<641px).
- **Active_Housemate**: The housemate currently selected in the session, identified by `activeHousemateId` in localStorage.
- **Avatar**: A 36×36px colored rounded square displaying the first letter of a housemate's name in white, using the housemate's assigned color as background.
- **Locale_Switcher**: A pair of EN/NL buttons allowing the user to switch the application language.
- **Date_Navigation_Panel**: A floating panel with previous/next day arrows and a contextual date title.
- **Dish_Panel**: The section displaying the current day's planned dish with inline editing.
- **Attendance_Section**: The section listing all housemates with three-state attendance toggles.
- **Comments_Section**: The section displaying housemate comments with inline editing for the active housemate.
- **History_Section**: The section displaying an audit log of changes for the day.
- **Nudge_Modal**: A modal overlay for sending push notification reminders to housemates.
- **Happie_Logo**: A green (#4CAF50) rounded square with a white bold "H" character, matching the login page brand element.

## Requirements

### Requirement 1: Desktop Sidebar Branding

> See [mockup-day-plan.png](./mockup-day-plan.png) for the visual reference of the full desktop layout including sidebar.

**User Story:** As a user, I want to see the Happie brand identity in the sidebar, so that the app feels polished and recognizable.

#### Acceptance Criteria

1. THE Sidebar SHALL always display the Happie_Logo (green rounded square with white "H") to the left of the text "Happie" as the topmost element in the sidebar, and the "Happie" text SHALL always appear alongside the logo.
2. THE Sidebar SHALL NOT display the text "Happie.Web" regardless of whether the "Happie" text is shown.

### Requirement 2: Desktop Sidebar Navigation Items

**User Story:** As a user, I want a clean navigation menu with relevant links, so that I can navigate the app without clutter.

#### Acceptance Criteria

1. THE Sidebar SHALL NOT display the "Counter" menu item.
2. THE Sidebar SHALL NOT display the "Weather" menu item.
3. THE Sidebar SHALL NOT display the "Home" menu item.
4. THE Sidebar SHALL display a menu item labeled "On the menu" that navigates to the Day Plan page for today's date (`/day/{today}`).
5. THE Sidebar SHALL display a menu item labeled "Calendar" that navigates to the Day Plan page for today's date (`/day/{today}`) until its dedicated page is implemented.
6. THE Sidebar SHALL display a menu item labeled "Housemates" that navigates to the Day Plan page for today's date (`/day/{today}`) until its dedicated page is implemented.
7. THE Sidebar SHALL display the menu items in the order: "On the menu", "Calendar", "Housemates". IF a menu item is removed, THEN the remaining items SHALL shift up to fill the gap.

### Requirement 3: Desktop Sidebar Active Housemate Avatar

**User Story:** As a user, I want to see my avatar in the sidebar, so that I know which housemate profile is active.

#### Acceptance Criteria

1. THE Sidebar SHALL display the Active_Housemate's Avatar in the bottom-left area without the housemate's name.
2. WHEN the user hovers over the Active_Housemate's Avatar, THE Sidebar SHALL display a green outline (`#4CAF50`) around the Avatar and change the cursor to a pointer.
3. WHEN the user clicks the Active_Housemate's Avatar, THE App SHALL navigate to the Housemates page (`/housemates`). (Note: this page does not exist yet.)

### Requirement 4: Desktop Sidebar Logout Link

**User Story:** As a user, I want access to a logout option, so that I can end my session.

#### Acceptance Criteria

1. THE Sidebar SHALL display a "Log Out" link below the navigation menu items.
2. WHEN the user clicks the "Log Out" link, THE App SHALL remove the `jwt` and `activeHousemateId` entries from localStorage and navigate to the login page (`/`).

### Requirement 5: Remove Top Header Bar

**User Story:** As a user, I want a cleaner layout without the top header bar, so that more vertical space is available for content.

#### Acceptance Criteria

1. WHILE the viewport width is 641px or greater, THE App SHALL NOT render the top header bar (containing the Locale_Switcher and logout button).

### Requirement 6: Desktop Sidebar Locale Switcher

**User Story:** As a user, I want to switch languages from the sidebar, so that I can change the locale without a separate header.

#### Acceptance Criteria

1. THE Sidebar SHALL display the Locale_Switcher in the bottom-left area as two buttons labeled "EN" and "NL", styled identically to the login page locale buttons (including a green highlight on hover and a visually distinct active state on the button matching the current locale).
2. WHEN the user clicks a locale button, THE App SHALL persist the selected locale, set it as the active locale, and reload the page as a single atomic operation so the UI renders in the newly selected language. IF any step fails, THEN THE App SHALL roll back the entire operation and leave the previous locale active.
3. IF the user clicks the locale button that is already active, THEN THE App SHALL not reload the page.

### Requirement 7: Desktop Sidebar Tagline

**User Story:** As a user, I want to see a brief description of the app, so that the purpose of Happie is clear.

#### Acceptance Criteria

1. THE Sidebar SHALL display an element with rounded corners and muted text styling (smaller font size and reduced contrast compared to navigation items) above the Locale_Switcher containing the text: "Happie - Coordinate dinner with your housemates effortlessly."

### Requirement 8: Mobile Header

> See [mockup-day-plan-mobile.png](./mockup-day-plan-mobile.png) for the visual reference.

**User Story:** As a mobile user, I want a compact header, so that I can see my identity and switch languages without a sidebar.

#### Acceptance Criteria

1. WHILE the viewport width is less than 641px, THE App SHALL display a mobile header fixed at the top of the viewport that remains visible when the page content scrolls.
2. THE mobile header SHALL display the text "Happie" in the top-left area without the Happie_Logo.
3. THE mobile header SHALL display the Active_Housemate's Avatar in the top-right area.
4. WHEN the user clicks the Active_Housemate's Avatar in the mobile header, THE App SHALL navigate to the Housemates page (`/housemates`). IF the user simultaneously clicks both the avatar and a locale button, THEN THE App SHALL prioritize the locale switch over navigation.
5. THE mobile header SHALL display the Locale_Switcher to the left of the Avatar, styled identically to the login page locale buttons.
6. WHEN the user clicks a locale button in the mobile header, THE App SHALL switch the active locale and reload the page. Only locale button clicks SHALL trigger a page reload; clicks on other mobile header elements (such as the avatar) SHALL NOT reload the page.
7. WHILE the viewport width is less than 641px, THE App SHALL NOT display the Sidebar.

### Requirement 9: Mobile Bottom Navigation Bar

**User Story:** As a mobile user, I want a bottom navigation bar, so that I can navigate between pages with my thumb.

#### Acceptance Criteria

1. WHILE the viewport width is less than 641px, THE App SHALL display a floating Bottom_Navigation_Bar fixed at the bottom of the viewport.
2. THE Bottom_Navigation_Bar SHALL contain icons in the following order: "On the menu" (home/day plan), "Calendar", "Housemates".
3. THE Bottom_Navigation_Bar SHALL NOT contain a logout icon.
4. WHEN the user taps the "On the menu" icon, THE App SHALL navigate to the Day Plan page for today's date.
5. THE "Calendar" and "Housemates" icons SHALL be styled as interactive items and navigate to the Day Plan page for today's date (`/day/{today}`) until their dedicated pages are implemented.
6. WHILE the Bottom_Navigation_Bar is displayed, THE Bottom_Navigation_Bar SHALL visually indicate the currently active page by highlighting the corresponding icon.

### Requirement 10: Date Navigation Panel

**User Story:** As a user, I want to navigate between days and see contextual date labels, so that I know which day I am viewing.

#### Acceptance Criteria

1. THE Date_Navigation_Panel SHALL display a left arrow button for navigating to the previous day and a right arrow button for navigating to the next day.
2. WHEN the user clicks the left arrow button, THE App SHALL navigate to the Day Plan page for the previous day relative to the currently viewed date.
3. WHEN the user clicks the right arrow button, THE App SHALL navigate to the Day Plan page for the next day relative to the currently viewed date.
4. WHEN the viewed date is today, THE Date_Navigation_Panel SHALL display the title "Today" (bold) on the first line and the formatted date (day month-abbreviation year, e.g. "18 Jun 2025") on the second line, localized to the active locale.
5. WHEN the viewed date is yesterday, THE Date_Navigation_Panel SHALL display the title "Yesterday" (bold) on the first line and the formatted date (day month-abbreviation year) on the second line, localized to the active locale.
6. WHEN the viewed date is tomorrow, THE Date_Navigation_Panel SHALL display the title "Tomorrow" (bold) on the first line and the formatted date (day month-abbreviation year) on the second line, localized to the active locale.
7. WHEN the viewed date is between 2 and 6 days in the past or future from today (excluding today, yesterday, and tomorrow), THE Date_Navigation_Panel SHALL display the day name (e.g. "Wednesday") as the title (bold) on the first line and the formatted date (day month-abbreviation year) on the second line, localized to the active locale.
8. WHEN the viewed date is 7 or more days from today (including exactly 7 days), THE Date_Navigation_Panel SHALL always hide the title line regardless of any other display logic and display the formatted date (day month-abbreviation year) in bold as the only line, localized to the active locale.
9. THE date format SHALL use the locale-aware abbreviated month name (e.g. "Jun" in English, "jun" in Dutch) by leveraging the runtime's built-in date formatting with the active locale, rather than hardcoding month abbreviations in resource files.
10. THE Date_Navigation_Panel SHALL appear as a floating panel with rounded corners.

### Requirement 11: Dish Panel Display

> See [mockup-day-plan.png](./mockup-day-plan.png) for the display state and [mockup-edit-dish.png](./mockup-edit-dish.png) for the editing state.

**User Story:** As a user, I want to see what's for dinner, so that I can plan my evening.

#### Acceptance Criteria

1. THE Dish_Panel SHALL display a food icon and the text "on the menu".
2. IF no dish has been entered for the day, THEN THE Dish_Panel SHALL display the text "What are we eating?" with an edit icon.
3. IF a dish has been entered for the day, THEN THE Dish_Panel SHALL display the dish text (up to 100 characters) instead of "What are we eating?" and show the name of the housemate who last edited it and a relative time indicator below the dish text.
4. IF a dish has been entered for the day, THEN THE Dish_Panel SHALL display an edit icon next to the dish text.
5. THE Dish_Panel SHALL format the relative time indicator as follows: "just now" for less than 60 seconds ago, "{N} min ago" for less than 60 minutes ago, "{N} hours ago" for less than 3 hours ago, the time in HH:mm format for 3 hours or more ago on the same calendar day, and the date (day month-abbreviation) followed by the time in HH:mm format for edits made on a previous calendar day.

### Requirement 12: Dish Panel Editing

**User Story:** As a user, I want to edit the dish inline, so that I can quickly update what's for dinner.

#### Acceptance Criteria

1. WHEN the user clicks the edit icon, THE Dish_Panel SHALL hide the edit icon, display a text input field pre-populated with the current dish text (or empty if no dish has been entered), enforce a maximum length of 100 characters on the input, and display accept and discard buttons.
2. WHEN the user clicks the accept button and the trimmed input is not empty, THE Dish_Panel SHALL save the trimmed dish text and return to display mode.
3. WHEN the user clicks the accept button and the trimmed input is empty, THE Dish_Panel SHALL clear the dish for the day and return to display mode.
4. WHEN the user clicks the discard button, THE Dish_Panel SHALL discard changes and return to display mode.
5. IF saving the dish fails, THEN THE Dish_Panel SHALL revert the dish text to its previous value and display an error notification.

### Requirement 13: Attendance Section

**User Story:** As a user, I want to see and set attendance for all housemates, so that everyone knows who is eating in.

#### Acceptance Criteria

1. THE Attendance_Section SHALL display a small header with the text "Attendance".
2. THE Attendance_Section SHALL list all active housemates using the same layout as the housemate selection on the login page (Avatar with name).
3. THE Attendance_Section SHALL display three option buttons for each housemate: V (check), ? (unknown), X (not eating).
4. WHEN a housemate's attendance status is "EatingIn", THE Attendance_Section SHALL highlight the V button in green (#4CAF50).
5. WHEN a housemate's attendance status is "NotEatingIn", THE Attendance_Section SHALL highlight the X button in red (#F44336).
6. WHEN a housemate's attendance status is "Unknown", THE Attendance_Section SHALL display the ? button in a neutral style (no color highlight).
7. WHEN the user clicks one of the three option buttons for a housemate, THE Attendance_Section SHALL optimistically update the UI to reflect the new status and send the update to the API.
8. IF the attendance update API call fails or cannot be sent (e.g. due to network issues), THEN THE Attendance_Section SHALL revert the button to the previous status and display an error notification.

### Requirement 14: Comments Section

> See [mockup-comments.png](./mockup-comments.png) for the display state and [mockup-edit-comments.png](./mockup-edit-comments.png) for the editing state.

**User Story:** As a user, I want to see comments from housemates and add my own, so that we can communicate about dinner plans.

#### Acceptance Criteria

1. THE Comments_Section SHALL display a small header with the text "Comments".
2. THE Comments_Section SHALL display all placed comments ordered by last edited at the top.
3. THE Comments_Section SHALL display each comment with the housemate's Avatar, name, and comment text.
4. WHEN a housemate other than the Active_Housemate has not placed a comment, THE Comments_Section SHALL NOT display anything for that housemate.
5. WHEN the Active_Housemate has not placed a comment, THE Comments_Section SHALL display a dotted outline placeholder with the text "Add a comment..." for the Active_Housemate.
6. WHEN the user hovers over the Active_Housemate's comment placeholder, THE Comments_Section SHALL apply a green hover effect (#4CAF50) to the dotted outline.
7. WHEN the user clicks the Active_Housemate's comment placeholder, THE Comments_Section SHALL replace the dotted outline with a solid border, display a text input (max 200 characters), and show discard and save buttons.
8. WHEN the user clicks the save button and the trimmed input is not empty, THE Comments_Section SHALL save the comment and return to display mode.
9. WHEN the user clicks the discard button, THE Comments_Section SHALL discard changes and return to display mode.
10. WHEN the Active_Housemate has an existing comment displayed, THE Comments_Section SHALL allow the user to click on it to enter edit mode with the existing text always pre-populated in the input field.
11. IF saving the comment fails, THEN THE Comments_Section SHALL revert to the previous state and display an error notification.

### Requirement 15: History Section

**User Story:** As a user, I want to see a log of changes, so that I know who changed what and when.

#### Acceptance Criteria

1. THE History_Section SHALL display a small header with a back-in-time icon and the text "History".
2. THE History_Section SHALL display change entries in reverse-chronological order (most recent first).
3. THE History_Section SHALL display each change entry with the housemate's Avatar and name, a grey clock icon, and the formatted timestamp.
4. WHEN the change occurred on the current calendar day (today), THE History_Section SHALL display only the time in HH:mm format (e.g. "18:30"). Each entry SHALL use its appropriate format based on when it occurred, even when displaying multiple entries from different time periods simultaneously.
5. WHEN the change occurred on a different day within the current calendar year, THE History_Section SHALL display the day, abbreviated month, and time (e.g. "15 Jun 18:30").
6. WHEN the change occurred in a previous calendar year, THE History_Section SHALL display the day, abbreviated month, year, and time (e.g. "15 Jun 2024 18:30").
7. THE History_Section SHALL display a description of what was changed on the line below the housemate name and timestamp.
8. IF no history entries exist for the specific viewed day, THEN THE History_Section SHALL display a message indicating that no changes have been recorded. This message SHALL only appear when viewing a specific day that has no changes.

### Requirement 16: Nudge Button Placement

**User Story:** As a user, I want quick access to the nudge feature, so that I can remind housemates to fill in their attendance.

#### Acceptance Criteria

1. THE App SHALL display a Nudge button on the same row as the Attendance_Section header, aligned to the right end of the row.
2. THE Nudge button SHALL contain a bell icon and the text "Nudge".
3. THE Nudge button SHALL be vertically centered relative to the Attendance_Section header text.

### Requirement 17: Nudge Modal

> See [mockup-nudge-modal.png](./mockup-nudge-modal.png) for the visual reference.

**User Story:** As a user, I want to select recipients and a message for my nudge, so that I can send targeted reminders.

#### Acceptance Criteria

1. WHEN the user clicks the Nudge button, THE App SHALL display the Nudge_Modal as an overlay with a blurred background.
2. THE Nudge_Modal SHALL display a bell icon and "Send a nudge" text in the top-left area, and an X close button in the top-right area.
3. THE Nudge_Modal SHALL display a "Who should we remind?" header followed by a list of all housemates (except the Active_Housemate) whose attendance status is "Unknown" for the viewed day, with all selected by default.
4. THE Nudge_Modal SHALL display "Predefined" and "Custom" message options, with "Predefined" selected by default.
5. WHEN "Predefined" is selected, THE Nudge_Modal SHALL display the 3 predefined nudge messages as selectable options with the first option selected by default.
6. WHEN "Custom" is selected, THE Nudge_Modal SHALL display a text input field for a custom message with a maximum length of 20 characters.
7. THE Nudge_Modal SHALL display a green button with a paper airplane icon and "Send Nudge" text at the bottom.
8. IF no recipients are selected, THEN THE Nudge_Modal SHALL disable the "Send Nudge" button and prevent all nudge sending regardless of the method used (including keyboard shortcuts or direct API calls).
9. WHEN the user clicks the "Send Nudge" button, THE Nudge_Modal SHALL send the nudge to the selected recipients with the chosen message and close the modal.
10. WHEN the user clicks the X close button, THE Nudge_Modal SHALL close without sending.

### Requirement 18: Day Plan Page Content Centering

**User Story:** As a user, I want the day plan content to be centered and well-spaced, so that the page is easy to read.

#### Acceptance Criteria

1. WHILE the viewport width is 641px or greater, THE Day Plan page content SHALL be horizontally centered within the available space (total viewport width minus the sidebar width) with a maximum content width of 600px. WHEN the available space is within the 600px maximum, THE content SHALL use all available space.
2. WHILE the viewport width is less than 641px, THE Day Plan page content SHALL use the full viewport width with 16px horizontal padding on each side.
3. WHILE the viewport width is 641px or greater and the available space exceeds 600px, THE Day Plan page content SHALL maintain equal horizontal margins on both sides of the content area.
