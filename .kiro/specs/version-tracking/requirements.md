# Requirements Document

## Introduction

The version-tracking feature records which Happie app version each housemate is running. Because the app is a PWA with a service worker that may remain on an older version between releases, knowing each housemate's active version lets the household admin understand whether users have already seen new features. The version is persisted once per session start on the housemates table in Azure Table Storage.

## Glossary

- **App_Version**: The semantic version string stamped into `appsettings.json` during the GitHub Actions deploy pipeline (format: `{major}.{minor}.{patch}.{run_number}`, e.g. "1.2.0.47"). Locally it is always "1.0.0".
- **Session_Start**: The moment a user begins actively using the app. This occurs in two scenarios: (1) fresh login — when the user selects a housemate on the LoginPage after entering the household password, or (2) returning visit — when the app detects a valid JWT and existing ActiveHousemateId in localStorage and auto-redirects to the DayPlanPage.
- **Version_Tracker**: The client-side component responsible for reporting the App_Version to the backend at Session_Start.
- **Housemates_Table**: The Azure Table Storage table that stores housemate records, partitioned by HouseholdId with RowKey = HousemateId.
- **Production_Version**: Any App_Version value that is not equal to "1.0.0". Only production versions are persisted.

## Requirements

### Requirement 1: Record App Version at Session Start

**User Story:** As a household admin, I want to see which app version each housemate is running, so that I can understand whether they have received the latest features.

#### Acceptance Criteria

1. WHEN a Session_Start occurs for a housemate with a Production_Version, THE Version_Tracker SHALL send a single request containing the App_Version string to the `PUT /api/housemates/version` endpoint for that session.
2. WHEN the backend receives a version report, THE system SHALL persist the App_Version on the housemate's record in the Housemates_Table, overwriting any previously stored version.
3. IF the App_Version equals "1.0.0" at Session_Start, THEN THE Version_Tracker SHALL NOT send the version report to the backend.
4. THE Version_Tracker SHALL send the version report as a fire-and-forget background operation that does not add latency to page rendering or navigation transitions.

### Requirement 2: Restrict Version Reporting to Initial Session Only

**User Story:** As a developer testing the app, I want version reporting to be skipped when I switch housemates or log into a different household, so that my test actions do not pollute version data.

#### Acceptance Criteria

1. WHEN the user switches the active housemate on the HousematesPage, THE Version_Tracker SHALL NOT send a version report for the newly selected housemate.
2. WHEN the user logs out and logs back in during the same browser session (including into a different household), THE Version_Tracker SHALL NOT send a version report for any subsequent Session_Start.
3. THE Version_Tracker SHALL use an in-memory flag, set upon initiating the first report (not upon success), that is reset only on full page reload or app restart. This ensures the version is reported at most once per app lifecycle.

### Requirement 3: Backend API for Version Reporting

**User Story:** As a developer, I want a dedicated API endpoint for receiving version reports, so that the version update is decoupled from other housemate operations.

#### Acceptance Criteria

1. THE system SHALL expose a `PUT /api/housemates/version` endpoint that accepts a JSON body containing a required, non-empty App_Version string with a maximum length of 20 characters.
2. WHEN the endpoint receives a valid request with a Production_Version, THE system SHALL update the `AppVersion` field on the housemate's record in the Housemates_Table identified by the HouseholdId from the JWT and the HousemateId from the `X-Housemate-Id` header, and return a 204 No Content response with no body.
3. WHEN the endpoint receives a request with App_Version equal to "1.0.0", THE system SHALL return a 204 No Content response without persisting the value.
4. IF the request body is missing, not valid JSON, or the App_Version field is null, empty, whitespace-only, or exceeds 20 characters, THEN THE system SHALL return a 422 Unprocessable Entity error with code VALIDATION_ERROR.
5. IF the housemate identified by the request headers does not exist or is soft-deleted, THEN THE system SHALL return a 404 Not Found error with code NOT_FOUND.
6. THE endpoint SHALL require authentication (valid JWT and X-Housemate-Id header), consistent with all other protected endpoints.

### Requirement 4: Housemate Entity Extension

**User Story:** As a household admin, I want the app version stored alongside other housemate data, so that I can query it without a separate data source.

#### Acceptance Criteria

1. THE Housemates_Table SHALL include an `AppVersion` column of type nullable string (`string?`) that stores the last reported Production_Version for each housemate, with a maximum length of 20 characters following semantic versioning format (e.g., "1.0.0", "2.13.4").
2. IF a housemate has never reported a version, THEN THE `AppVersion` property SHALL be null, resulting in the column being omitted from the Table Storage row.
3. THE `AppVersion` field SHALL be included in the housemate domain model and entity, but SHALL NOT be exposed in any existing API response (`HousemateDto` in `LoginResponse` and housemate list responses remain unchanged).
4. WHEN the housemate entity is persisted and subsequently retrieved, THE mapper SHALL round-trip the `AppVersion` value without data loss, mapping a null entity property to a null domain field and a non-null entity property to the corresponding domain field value.

### Requirement 5: Version Reporting Resilience

**User Story:** As a housemate, I want the version reporting to never interfere with my use of the app, so that a failure in reporting does not degrade my experience.

#### Acceptance Criteria

1. IF the version report API call fails (network error, server error, or timeout), THEN THE Version_Tracker SHALL catch the exception, discard the failure, and continue without retrying, logging to the user, or displaying any error indicator.
2. IF the version report API call fails or is skipped, THEN THE Version_Tracker SHALL NOT prevent page rendering, navigation, or any other API call from completing.
3. IF the device has no network connectivity at the moment of Session_Start, THEN THE Version_Tracker SHALL skip the version report entirely without queuing, persisting, or scheduling a later attempt.
4. THE Version_Tracker SHALL enforce a timeout of 10 seconds on the version report HTTP request; if the timeout elapses, the call is treated as failed per criterion 1.
