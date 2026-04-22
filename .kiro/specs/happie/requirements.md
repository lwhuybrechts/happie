# Requirements Document

## Introduction

A Progressive Web App (PWA) for a household of a variable number of people to track who is joining for dinner on which day. Housemates can indicate per day whether they are eating in, fill in the planned dish, add comments, and remind each other via a push notification to fill in their attendance. The app runs as a Blazor WebAssembly PWA on Azure Static Web Apps, with Azure Functions as the backend, and Azure Table Storage as the database. Access is protected by a shared password known only to the housemates of that household. The UI supports both English and Dutch.

The data model is designed to support multiple households from the start: every Housemate, Day_Plan, and related record is scoped to a Household. Initially only one Household will be active, but the architecture must allow additional Households to be added later without structural changes. Household management (creating Households and setting their passwords) is out of scope for the app UI and is performed directly in the database by an administrator.

## Glossary

- **Happie**: The application as a whole
- **Household**: A group of Housemates who share access to a single instance of Happie data, identified by a unique Household_Password; all Housemates, Day_Plans, and related records are scoped to a Household
- **Household_Password**: The password associated with a specific Household, used to authenticate access to that Household's data; Households and their passwords are managed directly in the database and not through the app UI
- **Housemate**: One of the authorized users of Happie within a Household; the number of Housemates is variable and managed within the app
- **Day_Plan**: The data for one specific day: attendance per Housemate, dish, and comments
- **Attendance_Status**: The choice of a Housemate for a day: "eating in", "not eating in", or "unknown"
- **Dish**: The description of what will be eaten on a given day
- **Comment**: A free-text note from a Housemate for a specific day
- **Nudge**: A push notification by which one Housemate asks the other housemates to fill in their attendance status
- **Shared_Password**: The single password known to all Housemates of a Household, used to access Happie; equivalent to the Household_Password for that Household
- **Active_Housemate**: The currently selected Housemate identity for the session, chosen after entering the Shared_Password and remembered on the device across sessions
- **Active_Household**: The Household whose data is loaded for the current session, determined by which Household_Password the user entered
- **Deleted_Housemate**: A Housemate who has been soft-deleted; removed from active use and future Day_Plans but retained in the system so that their historical data (attendance records, comments) remains visible, displayed as "Name (deleted)"
- **Housemate_Color**: A unique color assigned to a Housemate within a household, used to visually identify the Housemate across the app
- **VAPID**: Voluntary Application Server Identification, the protocol for Web Push notifications
- **PWA**: Progressive Web App, a web application that can be installed on the home screen like a native app
- **Locale**: The active language setting of the UI, either English ("en") or Dutch ("nl")
- **Calendar_View**: A calendar-style overview screen showing all days (past and future) with Housemate_Color indicators for attendance per day

---

## Requirements

### Requirement 1: Authentication and Access Control

**User Story:** As a housemate, I want only the authorized residents to have access to Happie, so that the data remains private.

#### Acceptance Criteria

1. WHEN a user opens Happie, THE Happie SHALL present a password entry screen before granting access.
2. WHEN a user enters a password that matches a known Household_Password, THE Happie SHALL set the Active_Household to the corresponding Household and present a list of all active Housemates in that Household for identity selection.
3. WHEN a user selects a Housemate name from the list, THE Happie SHALL store the selection as the Active_Housemate for the session and grant access.
4. THE Happie SHALL persist the Active_Housemate selection on the device so that the Housemate does not have to re-select their name on subsequent visits.
5. THE Happie SHALL attribute all actions (comments, nudges, attendance changes, push notifications) to the Active_Housemate within the Active_Household.
6. IF a user enters a password that does not match any known Household_Password, THEN THE Happie SHALL deny access and display a message that the password is incorrect.
7. WHEN a Housemate logs out, THE Happie SHALL end the session and redirect the Housemate to the login page.
8. THE Happie SHALL scope all data access (Housemates, Day_Plans, Comments, Attendance_Statuses) to the Active_Household so that data from different Households is never mixed.

---

### Requirement 2: Household Management (Out of Scope for App UI)

**User Story:** As an administrator, I want to manage Households and their passwords directly in the database, so that the app does not need to expose household administration functionality.

#### Acceptance Criteria

1. THE Happie SHALL not provide any UI for creating, modifying, or deleting Households or Household_Passwords.
2. THE Happie SHALL support multiple Households in the data model, each with their own Housemates, Day_Plans, and Household_Password, so that additional Households can be activated by inserting records directly in the database without requiring code changes.
3. THE Happie SHALL treat each Household as an isolated data scope: a Housemate of one Household SHALL have no access to the data of another Household.

---

### Requirement 3: Viewing the Day Plan

**User Story:** As a housemate, I want to see the Day_Plan for any day — past, present, or future — so that I can review what happened and plan ahead.

#### Acceptance Criteria

1. WHEN a Housemate opens Happie, THE Happie SHALL display the Day_Plan for today as the default view.
2. WHEN a Housemate swipes left on the Day_Plan view, THE Happie SHALL navigate to the Day_Plan for the next day.
3. WHEN a Housemate swipes right on the Day_Plan view, THE Happie SHALL navigate to the Day_Plan for the previous day.
4. THE Happie SHALL display the Attendance_Status of all active Housemates per day.
5. THE Happie SHALL display the filled-in Dish per day, or an empty state if no Dish has been filled in yet.
6. THE Happie SHALL display all Comments from Housemates per day.
7. THE Happie SHALL allow browsing to past days so that historical Day_Plans remain accessible.
8. THE Happie SHALL display each Housemate's Attendance_Status using that Housemate's Housemate_Color, so that it is immediately visible who is eating in on a given day.

---

### Requirement 4: Updating Attendance Status

**User Story:** As a housemate, I want to be able to indicate per day whether I am eating in, so that the other housemates know what to account for.

#### Acceptance Criteria

1. WHEN a Housemate selects an Attendance_Status for a day, THE Happie SHALL save the choice and make it immediately visible to all Housemates.
2. THE Happie SHALL offer the following status options: "eating in", "not eating in", and "unknown".
3. WHEN a Housemate changes a previously filled-in Attendance_Status, THE Happie SHALL save the new status and overwrite the previous status.
4. THE Happie SHALL allow any Housemate to change the Attendance_Status of any Housemate for a given day.
5. IF saving an Attendance_Status fails, THEN THE Happie SHALL display an error message and restore the previous status in the view.

---

### Requirement 5: Filling in a Dish

**User Story:** As a housemate, I want to be able to fill in what will be eaten, so that everyone knows what is on the menu.

#### Acceptance Criteria

1. WHEN a Housemate fills in a Dish for a day, THE Happie SHALL save the Dish and make it immediately visible to all Housemates.
2. WHEN a Housemate changes an existing Dish, THE Happie SHALL save the new Dish and overwrite the previous Dish.
3. THE Happie SHALL allow every Housemate to fill in or change the Dish for a day.
4. THE Happie SHALL accept a Dish of at most 100 characters.
5. IF saving a Dish fails, THEN THE Happie SHALL display an error message and preserve the input so the Housemate can try again.

---

### Requirement 6: Adding Comments

**User Story:** As a housemate, I want to be able to add a comment to a day, so that I can share extra information such as a dish preference or that I will be home late.

#### Acceptance Criteria

1. WHEN a Housemate adds a Comment to a day, THE Happie SHALL save the Comment and make it immediately visible to all Housemates.
2. THE Happie SHALL store at most one Comment per Housemate per day.
3. WHEN a Housemate changes an existing Comment, THE Happie SHALL save the new Comment and overwrite the previous Comment.
4. WHEN a Housemate deletes an existing Comment, THE Happie SHALL remove the Comment from the Day_Plan.
5. THE Happie SHALL accept a Comment of at most 200 characters.
6. IF saving a Comment fails, THEN THE Happie SHALL display an error message and preserve the input so the Housemate can try again.

---

### Requirement 7: Nudge — Reminder via Push Notification

**User Story:** As a housemate, I want to be able to send the other housemates a push notification to ask them to fill in their attendance, so that I know how many people are eating in.

#### Acceptance Criteria

1. WHEN a Housemate sends a Nudge, THE Happie SHALL send a push notification to the other Housemates via the VAPID Web Push protocol.
2. THE Happie SHALL include in the push notification which Housemate sent the Nudge and for which day attendance is being requested.
3. WHEN a Housemate taps the push notification, THE Happie SHALL open on the Day_Plan for the relevant day.
4. THE Happie SHALL only send a Nudge to Housemates whose Attendance_Status is still "unknown".
5. IF sending a push notification fails for a Housemate, THEN THE Happie SHALL inform the sending Housemate that the notification could not be delivered.

---

### Requirement 8: Push Notification Permission

**User Story:** As a housemate, I want to be able to grant permission for push notifications, so that I can receive Nudges on my iPhone.

#### Acceptance Criteria

1. WHEN a Housemate opens Happie for the first time after installing it as a PWA, THE Happie SHALL request permission to receive push notifications.
2. WHEN a Housemate grants permission for push notifications, THE Happie SHALL register the push subscription with the backend so that Nudges can be delivered.
3. IF a Housemate denies permission for push notifications, THEN THE Happie SHALL inform the Housemate that Nudges cannot be received without permission.
4. WHILE a Housemate has a valid push subscription, THE Happie SHALL renew the subscription when the browser creates a new subscription.

---

### Requirement 9: Offline Availability

**User Story:** As a housemate, I want to be able to consult Happie without an internet connection, so that I can always view the overview.

#### Acceptance Criteria

1. WHILE Happie is installed as a PWA, THE Happie SHALL make the most recently loaded Day_Plans available without an internet connection.
2. WHILE a Housemate is offline, THE Happie SHALL clearly indicate that the displayed data may not be up to date.
3. WHILE a Housemate is offline, THE Happie SHALL store the Housemate's input locally and synchronize it once the connection is restored.
4. WHEN the connection is restored after an offline period, THE Happie SHALL synchronize the locally stored changes with the backend.

---

### Requirement 10: Automatic Push Notification on Day Plan Changes

**User Story:** As a housemate, I want to be automatically notified when someone changes the day plan for today or tomorrow, so that I am always aware of last-minute updates without having to check the app.

#### Acceptance Criteria

1. WHEN a Housemate changes the Day_Plan for today or tomorrow, THE Happie SHALL automatically send a push notification to all other active Housemates via the VAPID Web Push protocol.
2. THE Happie SHALL include in the automatic push notification which Housemate made the change, which day is affected, and what was changed.
3. THE Happie SHALL not send an automatic push notification to the Housemate who made the change.
4. WHEN a Housemate taps the automatic push notification, THE Happie SHALL open on the Day_Plan for the relevant day.
5. IF sending an automatic push notification fails for a Housemate, THEN THE Happie SHALL log the failure without interrupting the save operation.

---

### Requirement 11: Language Selection (i18n)

**User Story:** As a housemate, I want to be able to switch the UI language between English and Dutch, so that I can use the app in my preferred language.

#### Acceptance Criteria

1. THE Happie SHALL support English ("en") and Dutch ("nl") as available Locales for all visible UI text.
2. WHEN a Housemate selects a Locale, THE Happie SHALL immediately display all UI text in the selected language without requiring a page reload.
3. THE Happie SHALL persist the selected Locale across sessions so that the Housemate's language preference is remembered.
4. IF no Locale has been set, THEN THE Happie SHALL default to Dutch ("nl").
5. THE Happie SHALL keep all source code, identifiers, and comments in English regardless of the active Locale.

---

### Requirement 12: Housemate Management

**User Story:** As a housemate, I want to manage the list of housemates, so that the app stays up to date as people move in or out.

#### Acceptance Criteria

1. WHEN a Housemate opens the housemate management screen, THE Happie SHALL display a list of all active Housemates.
2. WHEN a Housemate selects a different Housemate name from the management screen, THE Happie SHALL switch the Active_Housemate to the selected Housemate without requiring the Shared_Password to be re-entered.
3. WHEN a Housemate adds a new Housemate by providing a name, THE Happie SHALL add the new Housemate to the active Housemate list and make the new Housemate immediately available for selection.
4. THE Happie SHALL require only a name (1–50 characters, trimmed, not empty) when adding a new Housemate.
5. IF a Housemate is removed and has no linked attendance records or comments, THEN THE Happie SHALL permanently delete the Housemate from the system.
6. IF a Housemate is removed and has at least one linked attendance record or comment, THEN THE Happie SHALL soft-delete the Housemate: remove the Housemate from the active Housemate list and from all future Day_Plans, while retaining the Housemate's historical data.
7. WHILE a Housemate has been soft-deleted, THE Happie SHALL display the Housemate's name as "Name (deleted)" wherever their historical data (attendance records, comments) appears.
8. THE Happie SHALL not include Deleted_Housemates in the Active_Housemate selection list or in new Day_Plans.
9. IF saving a housemate management change (add or remove) fails, THEN THE Happie SHALL display an error message and leave the Housemate list unchanged.
10. WHEN a Housemate is added, THE Happie SHALL assign a default Housemate_Color that is not already in use within the household.
11. WHEN a Housemate changes the Housemate_Color of a Housemate via the management screen, THE Happie SHALL save the new Housemate_Color and immediately apply it throughout the app.
12. IF a Housemate selects a Housemate_Color that is already assigned to another Housemate in the same household, THEN THE Happie SHALL reject the selection and display a message that the color is already in use.
13. THE Happie SHALL allow duplicate Housemate names within a household, but SHALL enforce that all Housemate_Colors within a household are unique.
14. WHEN a Housemate renames a Housemate via the management screen, THE Happie SHALL save the new name and immediately display the updated name throughout the app.
15. IF saving a rename fails, THEN THE Happie SHALL display an error message and leave the Housemate's name unchanged.

---

### Requirement 13: Calendar View

**User Story:** As a housemate, I want a calendar overview of all days, so that I can see attendance at a glance and quickly navigate to any day.

#### Acceptance Criteria

1. THE Happie SHALL provide a Calendar_View that displays all days, both past and future, in a calendar layout.
2. THE Happie SHALL display the Housemate_Colors of all Housemates whose Attendance_Status is "eating in" on each day in the Calendar_View, so that attendance is visible at a glance.
3. WHEN a Housemate taps a day in the Calendar_View, THE Happie SHALL navigate to the Day_Plan view for that day.
4. IF no Housemate has an Attendance_Status of "eating in" on a given day, THEN THE Happie SHALL display that day in the Calendar_View without any Housemate_Color indicators.
