# Implementation Plan: Version Tracking

## Overview

This plan implements version tracking by extending the housemate data model with a nullable `AppVersion` field, adding a `PUT /api/housemates/version` backend endpoint with validation and persistence logic, and creating a client-side `VersionTracker` service that fires a single fire-and-forget version report on the first session start per app lifecycle. The implementation touches the domain model, entity, mapper, handler, function, a new shared contract, and a new Blazor service wired into the LoginPage.

## Tasks

- [x] 1. Extend housemate data model and mapper
  - [x] 1.1 Add `AppVersion` to `Housemate` domain record, `HousemateEntity`, and `HousemateMapper`
    - Add `string? AppVersion = null` parameter to the `Housemate` record in `Happie.Api/Domain/Housemate.cs`
    - Add `string? AppVersion { get; set; }` property to `HousemateEntity` in `Happie.Api/Infrastructure/Entities/HousemateEntity.cs`
    - Update `HousemateMapper.ToModel` to map `entity.AppVersion` → `housemate.AppVersion`
    - Update `HousemateMapper.ToEntity` to map `housemate.AppVersion` → `entity.AppVersion`
    - Verify that `HousemateDto` in `Happie.Shared/Contracts/` does NOT expose `AppVersion`
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 1.2 Write property test for mapper round-trip (Property 4)
    - **Property 4: Mapper round-trip preserves AppVersion**
    - Generate random `Housemate` records with `AppVersion` set to null or any string of 1–20 characters
    - Map to `HousemateEntity` and back via the mapper, assert `AppVersion` is identical
    - **Validates: Requirements 4.4**

- [x] 2. Implement backend version reporting endpoint
  - [x] 2.1 Create `ReportVersionRequest` contract in `Happie.Shared/Contracts/`
    - Create `ReportVersionRequest.cs` with a `Version` property annotated with `[Required]` and `[MaxLength(20)]`
    - Use `[JsonPropertyName("version")]` for wire format
    - _Requirements: 3.1, 3.4_

  - [x] 2.2 Create `ReportVersionOutcome` enum in `Happie.Api/Results/`
    - Define `Success`, `Skipped`, `NotFound`, `ValidationError` members
    - _Requirements: 3.2, 3.3, 3.4, 3.5_

  - [x] 2.3 Add `ReportVersionAsync` to `IHousemateHandler` and `HousemateHandler`
    - Add `Task<ReportVersionOutcome> ReportVersionAsync(Guid householdId, Guid housemateId, string version, CancellationToken cancellationToken = default)` to the interface
    - Implement logic: trim version → reject whitespace-only (ValidationError) → skip "1.0.0" (Skipped) → fetch housemate → reject not found/soft-deleted (NotFound) → update AppVersion → upsert → return Success
    - _Requirements: 1.2, 3.2, 3.3, 3.4, 3.5_

  - [x] 2.4 Write property test for version persistence (Property 1)
    - **Property 1: Version persistence overwrites any previous value**
    - Generate valid production version strings (1–20 chars, not "1.0.0") and housemate records with random existing AppVersion
    - Assert that after `ReportVersionAsync`, the upserted housemate has `AppVersion` equal to the trimmed input
    - **Validates: Requirements 1.2, 3.2**

  - [x] 2.5 Write property test for local dev version rejection (Property 2)
    - **Property 2: Local development version is never persisted**
    - Generate housemate records with any existing AppVersion (null or valid string)
    - Call handler with version "1.0.0", assert housemate record is unchanged and outcome is `Skipped`
    - **Validates: Requirements 3.3**

  - [x] 2.6 Write property test for invalid version rejection (Property 3)
    - **Property 3: Invalid version strings are rejected**
    - Generate strings that are null, empty, whitespace-only, or exceed 20 characters
    - Assert handler returns `ValidationError` and does not modify the housemate record
    - **Validates: Requirements 3.4**

  - [x] 2.7 Add `ReportVersionAsync` function method to `HousematesFunction`
    - Add `[Function("ReportVersion")]` method with `PUT` trigger on route `housemates/version`
    - Read and validate request body via `RequestValidator.ReadAndValidateAsync<ReportVersionRequest>`
    - Extract `householdId` from JWT and `housemateId` from `X-Housemate-Id` header
    - Delegate to `IHousemateHandler.ReportVersionAsync`
    - Map outcome: `Success`/`Skipped` → 204, `NotFound` → 404, `ValidationError` → 422
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement client-side VersionTracker service
  - [x] 4.1 Create `VersionTracker` service in `Happie.Web/Services/`
    - Create `VersionTracker.cs` as a scoped service
    - Inject `HttpClient`, `IConnectivityService`, `IConfiguration`
    - Implement `ReportVersionAsync()`: check in-memory `_hasReported` flag → check connectivity → read `AppVersion` from configuration → skip if "1.0.0" → set flag immediately → fire-and-forget PUT with 10-second timeout
    - Catch all exceptions in the background task, discard silently
    - _Requirements: 1.1, 1.3, 1.4, 2.3, 5.1, 5.2, 5.3, 5.4_

  - [x] 4.2 Write property test for at-most-once reporting (Property 5)
    - **Property 5: At-most-once reporting per app lifecycle**
    - Generate sequences of 1–10 `ReportVersionAsync` invocations on the same `VersionTracker` instance
    - Mock `HttpClient` and assert at most one HTTP request is initiated regardless of call count
    - **Validates: Requirements 2.3**

  - [x] 4.3 Register `VersionTracker` in DI and wire into `LoginPage`
    - Register `VersionTracker` as a scoped service in `Program.cs`
    - Inject `VersionTracker` in `LoginPage.razor`
    - Call `VersionTracker.ReportVersionAsync()` in `SelectHousemateAsync` after setting active housemate (fresh login path)
    - Call `VersionTracker.ReportVersionAsync()` in `OnInitializedAsync` when auto-redirecting (returning visit path)
    - Do NOT add any call on `HousematesPage` housemate switch
    - _Requirements: 1.1, 2.1, 2.2_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Unit tests for edge cases and integration points
  - [x] 6.1 Write unit tests for VersionTracker behavior
    - Test fresh login path fires exactly one HTTP request (Requirement 1.1)
    - Test auto-redirect path fires exactly one HTTP request (Requirement 1.1)
    - Test version "1.0.0" skipped on client side — no HTTP call (Requirement 1.3)
    - Test fire-and-forget does not block (Requirement 1.4)
    - Test housemate switch does not trigger report (Requirement 2.1)
    - Test logout + re-login in same session does not trigger second report (Requirement 2.2)
    - Test HTTP failure silently discarded, no retry (Requirement 5.1)
    - Test offline detection skips report entirely (Requirement 5.3)
    - Test 10-second timeout applied (Requirement 5.4)
    - _Requirements: 1.1, 1.3, 1.4, 2.1, 2.2, 5.1, 5.3, 5.4_

  - [x] 6.2 Write unit tests for backend handler edge cases
    - Test endpoint returns 404 for non-existent housemate (Requirement 3.5)
    - Test endpoint returns 404 for soft-deleted housemate (Requirement 3.5)
    - Test endpoint requires authentication (Requirement 3.6)
    - Test new housemate has null AppVersion by default (Requirement 4.2)
    - Test HousemateDto does not expose AppVersion field (Requirement 4.3)
    - _Requirements: 3.5, 3.6, 4.2, 4.3_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The `AppVersion` column is nullable in Azure Table Storage — null columns are omitted from the row, so never-reported housemates have no storage overhead
- The `VersionTracker` flag is set on initiation (not success) to guarantee at-most-once semantics without persistence
- Backend independently rejects "1.0.0" as defense in depth, even though the client also skips it

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "2.2"] },
    { "id": 1, "tasks": ["1.2", "2.3"] },
    { "id": 2, "tasks": ["2.4", "2.5", "2.6", "2.7"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["4.2", "4.3"] },
    { "id": 5, "tasks": ["6.1", "6.2"] }
  ]
}
```
