# Requirements Document

## Introduction

The DayHistory feature currently stores pre-rendered English text in the `Description` field of each history entry. This text is displayed in the `HistorySection` component and used as the body of automatic push notifications. Because the text is baked in at write time, it cannot be translated to the user's active locale. This feature replaces the pre-rendered description with a structured representation stored as separate entity fields (a `TranslationKey` string and a `Parameters` JSON string) so that history entries can be resolved into any supported locale at read time. A shared resolver and `.resx` resource files in the `Happie.Shared` project enable both the frontend and backend to resolve translations using the same code. The API returns the raw `translationKey` and `parameters` in the `HistoryEntryDto` so the frontend resolves client-side. The backend uses the same shared resolver for push notification bodies (which cannot be resolved client-side). The existing `NudgeMessageResolver` static class is migrated into the same shared `.resx` + resolver pattern. A one-time migration script converts all existing records to the new format.

## Glossary

- **History_Entry**: A single audit log record stored in the `DayHistory` table, recording a change made to a day plan.
- **Translation_Key**: A string identifier (e.g., `"history_attendance_set"`) that maps to a locale-specific message template containing parameter placeholders.
- **Parameters**: A JSON-serialized dictionary of named string values that are substituted into the message template at render time (e.g., `{"name":"Alice","status":"EatingIn"}`).
- **Shared_Resolver**: A component in `Happie.Shared` that accepts a Translation_Key, Parameters dictionary, and a locale, and produces a human-readable localized string. Used by both frontend and backend.
- **Shared_Resources**: The `.resx` resource files (`SharedStrings.resx` and `SharedStrings.en.resx`) located in `Happie.Shared/Resources/`, containing translation templates for history entries, nudge messages, and AttendanceStatus display names.
- **HistorySection**: The Blazor component that renders the audit log of changes for a given day.
- **Auto_Notification**: A push notification sent automatically to household members when a day plan field changes for today or tomorrow.
- **Locale**: One of the supported languages (`"en"` or `"nl"`).
- **Migration_Script**: A one-time dotnet-script (`.csx`) that converts existing DayHistory records from the old pre-rendered Description format to the new TranslationKey + Parameters fields.
- **NudgeMessageResolver**: The existing static class in `Happie.Api/Services/` with hardcoded switch expressions for 3 predefined nudge message keys, to be migrated to the shared resolver pattern.
- **AppStrings**: The frontend-only `.resx` resource files in `Happie.Web/Resources/` used by `IStringLocalizer<AppStrings>` for UI strings.

## Requirements

### Requirement 1: Store Structured Descriptions in Separate Entity Fields

**User Story:** As a developer, I want history entries to store a translation key and parameters as separate database fields, so that the description can be resolved into any locale at read time without parsing a combined JSON blob.

#### Acceptance Criteria

1. THE DayHistoryEntity SHALL have a `TranslationKey` string property containing the translation key identifier.
2. THE DayHistoryEntity SHALL have a `Parameters` string property containing a JSON-serialized dictionary of named string values.
3. WHEN a day plan change occurs, THE DayHandler SHALL create a History_Entry with the `TranslationKey` field set to the appropriate key and the `Parameters` field set to the JSON-serialized parameter dictionary.
4. THE Translation_Key SHALL be a non-empty string matching a key defined in the Shared_Resources files (`SharedStrings.resx` and `SharedStrings.en.resx`).
5. THE Parameters field SHALL contain a JSON object with all name-value pairs referenced as placeholders in the corresponding message template, with each value serialized as a string.
6. WHEN an attendance change is recorded, THE DayHandler SHALL store the Translation_Key `"history_attendance_set"` with parameters `name` (housemate name) and `status` (the AttendanceStatus enum member name, e.g. `"EatingIn"`, `"NotEatingIn"`, `"Unknown"`).
7. WHEN a dish change is recorded, THE DayHandler SHALL store the Translation_Key `"history_dish_set"` with parameter `description` (the dish text).
8. WHEN a comment is set, THE DayHandler SHALL store the Translation_Key `"history_comment_set"` with parameters `name` (housemate name) and `text` (the comment text).
9. WHEN a comment is deleted, THE DayHandler SHALL store the Translation_Key `"history_comment_deleted"` with parameter `name` (housemate name).
10. WHEN a chef status change is recorded, THE DayHandler SHALL store the Translation_Key `"history_chef_status_changed"` with parameters `name` (target housemate name) and `enabled` (string `"true"` or `"false"`).
11. THE old `Description` field SHALL be removed from the DayHistoryEntity.

### Requirement 2: Shared Resolver and Resource Files in Happie.Shared

**User Story:** As a developer, I want a single translation resolver and resource file set in the shared project, so that both frontend and backend resolve history and nudge translations using the exact same code and templates.

#### Acceptance Criteria

1. THE Shared_Resolver SHALL live in the `Happie.Shared` project so that both `Happie.Web` and `Happie.Api` can reference the same resolution logic.
2. THE Shared_Resources SHALL be `.resx` files (`SharedStrings.resx` for Dutch, `SharedStrings.en.resx` for English) located in `Happie.Shared/Resources/`.
3. THE Shared_Resources SHALL include a translation template for the `history_chef_status_changed` key in both Dutch and English, using `{name}` and `{enabled}` placeholders.
4. THE Shared_Resolver SHALL accept a Translation_Key, a Parameters dictionary, and a Locale, and return a human-readable localized string.
5. THE Shared_Resolver SHALL look up the Translation_Key in the Shared_Resources for the given Locale and substitute each `{parameterName}` placeholder with the corresponding value from the Parameters dictionary.
6. WHEN the Translation_Key references an AttendanceStatus value in the `status` parameter, THE Shared_Resolver SHALL replace the raw enum value with the localized AttendanceStatus display name (e.g., `"EatingIn"` resolves to `"Eating in"` in English or `"Mee-eten"` in Dutch).
7. IF a Translation_Key is not found in the Shared_Resources, THEN THE Shared_Resolver SHALL return the Translation_Key itself as the resolved string without failing.
8. IF the Parameters dictionary is null or empty, THEN THE Shared_Resolver SHALL return the message template without substitution.
9. THE Shared_Resolver SHALL use the same resolution logic regardless of whether it is invoked from the frontend or the backend.

### Requirement 3: Client-Side Resolution for UI Display

**User Story:** As a user, I want the history log to display in my active language immediately, so that I can understand what changes were made without waiting for a server round-trip to change locale.

#### Acceptance Criteria

1. THE HistoryEntryDto SHALL contain a `translationKey` string field and a `parameters` dictionary field (JSON-serialized as a string on the wire).
2. THE HistoryEntryDto SHALL NOT contain a pre-resolved `description` string field.
3. WHEN the API returns history entries, THE API SHALL return the raw `translationKey` and `parameters` values as stored in the database without performing resolution.
4. THE HistorySection component SHALL resolve each History_Entry into a human-readable string using the Shared_Resolver with the user's active locale.
5. WHEN the user switches locale, THE HistorySection component SHALL re-render all history entries in the new locale without requiring a new API call.

### Requirement 4: Server-Side Resolution for Push Notifications

**User Story:** As a user, I want push notifications to display in my language, so that I can understand what changed without opening the app.

#### Acceptance Criteria

1. WHEN an Auto_Notification is sent, THE PushHandler SHALL resolve the History_Entry into the recipient's stored Locale using the Shared_Resolver before including it in the push payload body.
2. THE PushHandler SHALL resolve independently per recipient so that each housemate receives the notification in their own locale.
3. THE PushHandler SHALL use the same Shared_Resolver and Shared_Resources that the frontend uses for client-side resolution.

### Requirement 5: Migrate NudgeMessageResolver to Shared Pattern

**User Story:** As a developer, I want the existing NudgeMessageResolver to use the same shared `.resx` + resolver pattern, so that all translation logic is consolidated in one place and new nudge messages only require adding resource file entries.

#### Acceptance Criteria

1. THE Shared_Resources SHALL contain translation templates for all existing predefined nudge message keys (`PleaseAddAttendance`, `WhatWouldYouLikeToEat`, `DinnerSoonWhatsYourPlan`).
2. THE PushHandler SHALL resolve predefined nudge messages using the Shared_Resolver with the recipient's stored Locale, replacing the current static `NudgeMessageResolver` class.
3. WHEN a nudge message template requires a date parameter, THE Shared_Resolver SHALL format the date according to the target Locale (e.g., `"d MMMM"` for Dutch, `"MMMM d"` for English).
4. THE existing `NudgeMessageResolver` static class in `Happie.Api/Services/` SHALL be removed after migration.
5. WHEN a new predefined nudge message key is added, THE developer SHALL only need to add entries to the Shared_Resources files and a new enum value to `NudgeMessageKey`, without modifying resolver code.

### Requirement 6: Prevent Accidental Frontend-Only Translations

**User Story:** As a developer, I want history and nudge translation strings to live exclusively in the shared project, so that they are not accidentally added to the frontend-only AppStrings resource files where they would be unreachable by the backend.

#### Acceptance Criteria

1. THE history translation keys (prefixed with `history_`) SHALL exist only in the Shared_Resources files in `Happie.Shared/Resources/`.
2. THE nudge translation keys (prefixed with `nudge_`) SHALL exist only in the Shared_Resources files in `Happie.Shared/Resources/`.
3. THE AttendanceStatus display name keys SHALL exist only in the Shared_Resources files in `Happie.Shared/Resources/`.
4. THE history, nudge, and AttendanceStatus display name keys SHALL NOT be added to the AppStrings resource files in `Happie.Web/Resources/`.
5. THE AppStrings resource files in `Happie.Web/Resources/` SHALL continue to hold UI-only strings (labels, headings, button text, error messages) that are not needed by the backend.

### Requirement 7: Translation Key Durability

**User Story:** As a developer, I want translation keys to remain stable over time, so that old history entries continue to render correctly even after translation text is updated.

#### Acceptance Criteria

1. THE system SHALL store each History_Entry with a Translation_Key and a Parameters JSON string, rather than a pre-rendered description string.
2. WHEN a translation template text is modified, THE developer SHALL keep the same set of named parameter placeholders (names and count) in the updated template so that all previously stored History_Entry records remain renderable.
3. IF a translation change requires adding, removing, or renaming parameter placeholders, THEN THE developer SHALL introduce a new Translation_Key and retain the old key with its original template unchanged.
4. THE system SHALL NOT delete or reassign existing Translation_Keys to a different template meaning once they have been persisted in any History_Entry record.
5. IF the Shared_Resolver encounters a History_Entry whose Translation_Key cannot be resolved, THEN THE Shared_Resolver SHALL render the raw Translation_Key and its stored parameter values as a fallback instead of failing or displaying an empty entry.

### Requirement 8: Localized AttendanceStatus Display Names

**User Story:** As a user, I want attendance status values in history entries to show human-readable names in my language, so that I see "Eating in" or "Mee-eten" instead of "EatingIn".

#### Acceptance Criteria

1. THE Shared_Resources SHALL define a localized display name for each AttendanceStatus enum value (Unknown, EatingIn, NotEatingIn) in both Dutch and English.
2. WHEN the Shared_Resolver resolves a `status` parameter for client-side display, THE Shared_Resolver SHALL use the user's active locale to select the corresponding display name.
3. WHEN the Shared_Resolver resolves a `status` parameter for a push notification, THE Shared_Resolver SHALL use the recipient's stored locale to select the corresponding display name.
4. IF the `status` parameter value does not match a known AttendanceStatus enum value, THEN THE Shared_Resolver SHALL display the raw parameter value unchanged.

### Requirement 9: Data Migration of Existing History Records

**User Story:** As a developer, I want all existing DayHistory records to be converted to the new TranslationKey + Parameters format, so that the system can assume all records use the new structure and no backwards-compatibility detection is needed.

#### Acceptance Criteria

1. THE Migration_Script SHALL be a dotnet-script (`.csx` file) located in `Happie.Api.IntegrationTests/Scripts/`, following the same pattern as `seed-local.csx`.
2. THE Migration_Script SHALL read all existing DayHistory records from Azure Table Storage and convert each record's pre-rendered Description text into the appropriate TranslationKey and Parameters fields.
3. THE Migration_Script SHALL parse the existing English description text to determine the correct Translation_Key and extract parameter values, including the chef status pattern `"{name}'s chef status enabled."` and `"{name}'s chef status disabled."` which map to Translation_Key `"history_chef_status_changed"` with parameters `name` and `enabled` (`"true"` for enabled, `"false"` for disabled).
4. THE Migration_Script SHALL write the TranslationKey and Parameters fields back to each entity and clear the old Description field.
5. THE Migration_Script SHALL be idempotent so that running it multiple times produces the same result.
6. THE Migration_Script SHALL log each converted record to the console for verification.
7. IF the Migration_Script cannot determine the correct Translation_Key for a record, THEN THE Migration_Script SHALL log a warning and skip that record without failing.
8. THE Migration_Script SHALL handle all 117 existing DayHistory records across all households.
