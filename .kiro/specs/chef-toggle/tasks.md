# Implementation Plan: Chef Toggle

## Overview

Add a chef toggle button to the attendance section of the day plan. The feature includes a new API endpoint for toggling chef status, updates to the data model to store `IsChef` on attendance records, optimistic UI in the frontend, auto-chef assignment when a dish is first entered, and history tracking for all chef status changes.

## Tasks

- [x] 1. Extend data model and shared contracts
  - [x] 1.1 Add `IsChef` property to `AttendanceRecord` domain record, `AttendanceRecordEntity`, and `AttendanceRecordMapper`
    - Add `bool IsChef` parameter to the `AttendanceRecord` record in `Happie.Api/Domain/AttendanceRecord.cs`
    - Add `bool IsChef` property to `AttendanceRecordEntity` in `Happie.Api/Infrastructure/Entities/AttendanceRecordEntity.cs`
    - Update `IAttendanceRecordMapper` and `AttendanceRecordMapper` to map `IsChef` in both `ToModel` and `ToEntity`
    - Fix all existing call sites that construct `AttendanceRecord` to include the new `IsChef` parameter
    - _Requirements: 7.1, 7.3_

  - [x] 1.2 Add `IsChef` to `AttendanceDto` and add `UpdateChefStatusRequest` contract
    - Add `bool IsChef` property (JSON name: `"isChef"`) to `AttendanceDto` in `Happie.Shared/Contracts/AttendanceDto.cs`
    - Create `UpdateChefStatusRequest` record in `Happie.Shared/Contracts/UpdateChefStatusRequest.cs` with a single `bool IsChef` property
    - _Requirements: 7.1, 2.1_

  - [x] 1.3 Add `ChefStatusChanged` value to `ChangeType` enum
    - Add `ChefStatusChanged` to the `ChangeType` enum in `Happie.Shared/Domain/ChangeType.cs`
    - _Requirements: 8.1_

- [x] 2. Implement repository and handler logic
  - [x] 2.1 Add `UpsertChefStatusAsync` to `IAttendanceRepository` and `AttendanceRepository`
    - Add method signature `Task UpsertChefStatusAsync(Guid householdId, DateOnly date, Guid housemateId, bool isChef, CancellationToken cancellationToken = default)` to `IAttendanceRepository`
    - Implement in `AttendanceRepository`: read existing entity, if exists update only `IsChef` and upsert, if not exists create new entity with `Status = AttendanceStatus.Unknown` and the given `IsChef` value
    - _Requirements: 2.1, 2.4, 4.4_

  - [x] 2.2 Update existing `UpsertAttendanceAsync` in `DayHandler` to preserve `IsChef`
    - When upserting attendance status, read the existing attendance record first to get the current `IsChef` value
    - Construct the new `AttendanceRecord` with the existing `IsChef` (defaulting to `false` if no record exists)
    - This ensures changing attendance never overwrites chef status
    - _Requirements: 2.3, 6.3_

  - [x] 2.3 Implement `UpsertChefStatusAsync` in `IDayHandler` and `DayHandler`
    - Add method `Task<bool> UpsertChefStatusAsync(Guid householdId, DateOnly date, Guid housemateId, bool isChef, Guid actingHousemateId, CancellationToken cancellationToken = default)` to `IDayHandler`
    - Implement in `DayHandler`: verify target housemate exists and is not soft-deleted, call `IAttendanceRepository.UpsertChefStatusAsync`, write a `DayHistory` entry with `ChangeType.ChefStatusChanged` and the acting housemate's ID, return `true` on success or `false` if housemate not found
    - _Requirements: 2.1, 4.1, 4.2, 4.3, 8.1, 8.2_

  - [x] 2.4 Update `GetDayPlanAsync` in `DayHandler` to include `IsChef` in `AttendanceDto`
    - When constructing `AttendanceDto` for each housemate, read `IsChef` from the attendance record (default `false` if no record exists)
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 2.5 Write property test: Chef toggle round-trip (last-write-wins)
    - **Property 1: Chef toggle round-trip (last-write-wins)**
    - **Validates: Requirements 2.1, 4.4**

  - [x] 2.6 Write property test: Chef status is independent of attendance status
    - **Property 2: Chef status is independent of attendance status**
    - **Validates: Requirements 2.2, 2.3, 6.3**

  - [x] 2.7 Write property test: Per-housemate chef independence
    - **Property 3: Per-housemate chef independence**
    - **Validates: Requirements 3.2, 7.3**

  - [x] 2.8 Write property test: Multiple chefs and cross-housemate toggling
    - **Property 4: Multiple chefs and cross-housemate toggling**
    - **Validates: Requirements 2.5, 2.6, 3.1, 3.3, 4.1**

  - [x] 2.9 Write property test: Chef toggle creates correctly attributed history entry
    - **Property 5: Chef toggle creates correctly attributed history entry**
    - **Validates: Requirements 4.2, 8.1, 8.2, 8.3**

- [x] 3. Implement API endpoint
  - [x] 3.1 Add `PutChefStatusAsync` function to `DaysFunction`
    - Add a new HTTP-triggered function: `PUT /api/days/{date}/chef/{housemateId}`
    - Parse route parameters using `RouteParser.TryParseDate` and `RouteParser.TryParseGuid`
    - Read and validate request body using `RequestValidator.ReadAndValidateAsync<UpdateChefStatusRequest>`
    - Extract `householdId` and `actingHousemateId` from the function context
    - Delegate to `IDayHandler.UpsertChefStatusAsync`
    - Return 204 No Content on success, 404 Not Found if housemate does not exist
    - _Requirements: 2.1, 4.1, 4.3_

  - [x] 3.2 Write unit tests for `PutChefStatusAsync` function
    - Test route parsing errors (invalid date, invalid GUID) return 400
    - Test missing/malformed request body returns 400/422
    - Test successful delegation returns 204
    - Test housemate not found returns 404
    - _Requirements: 4.3, 2.1_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement frontend chef toggle in AttendanceSection
  - [x] 5.1 Add chef toggle button to `AttendanceSection.razor`
    - Add a chef's hat icon button to the left of the three attendance radio buttons for each active housemate row
    - Implement optimistic UI pattern: `_chefOverrides` dictionary, `_chefSavingIds` hashset, `GetIsChef` method, `ToggleChefAsync` method
    - Apply active CSS class when `IsChef` is true, muted appearance when false
    - Set `aria-pressed` attribute to `"true"` or `"false"` based on chef status
    - Set `aria-label` using `IStringLocalizer<AppStrings>` with housemate name placeholder
    - Disable clicks while API call is in flight for that housemate
    - On API failure: roll back visual state and show error toast
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 9.1, 9.2, 9.3, 9.4_

  - [x] 5.2 Add scoped CSS for chef toggle in `AttendanceSection.razor.css`
    - Style the chef toggle button with active and inactive states
    - Ensure visual distinction between enabled (active class) and disabled (muted) states
    - _Requirements: 1.3, 1.4_

- [x] 6. Implement auto-chef assignment in DishPanel
  - [x] 6.1 Add auto-chef logic to `DishPanel.razor` after successful dish save
    - After a successful non-empty dish save, check if any housemate in the attendance list has `IsChef = true`
    - If no housemate is chef: fire `PUT /api/days/{date}/chef/{actingHousemateId}` with `{ "isChef": true }`
    - If at least one housemate is already chef: do nothing
    - If dish is cleared (empty after trim): do not trigger any chef status change
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3_

- [x] 7. Add localization strings
  - [x] 7.1 Add chef toggle localization keys to resource files
    - Add chef toggle aria-label string (with `{0}` placeholder for housemate name) to both `AppStrings.resx` (Dutch) and `AppStrings.en.resx` (English)
    - Add DayHistory description strings for manual chef toggle (enabled/disabled) and auto-chef assignment to both resource files
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The design specifies C# throughout (Blazor WebAssembly + Azure Functions), so all code uses C#
- Auto-chef logic lives entirely in the frontend; the server treats it as a regular chef toggle
- The `IsChef` field is merged into the existing `AttendanceRecords` table — no new table needed

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.3"] },
    { "id": 1, "tasks": ["1.2", "2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4"] },
    { "id": 3, "tasks": ["2.5", "2.6", "2.7", "2.8", "2.9", "3.1"] },
    { "id": 4, "tasks": ["3.2", "5.1", "7.1"] },
    { "id": 5, "tasks": ["5.2", "6.1"] }
  ]
}
```
