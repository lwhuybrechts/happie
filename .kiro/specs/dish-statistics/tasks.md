# Implementation Plan: Dish Statistics

## Overview

This implementation plan adds two statistics detail pages (DishDetailsPage and HousemateDetailsPage) with supporting API endpoints, statistics computation handlers, shared contract types, and reusable UI components (TimeRangeSelector, TimelineChart, DonutChart, OverflowMenu). The plan proceeds bottom-up: shared types and contracts first, then API handlers and functions, then client-side components and pages, and finally integration wiring.

## Tasks

- [x] 1. Define shared contracts and result types
  - [x] 1.1 Create dish statistics shared contract types
    - Create `DishStatisticsResponse.cs`, `DishTimelineDto.cs` in `Happie.Shared/Contracts/`
    - Each record uses `[JsonPropertyName]` attributes matching the wire format defined in design
    - _Requirements: 11.1, 11.3, 11.4_

  - [x] 1.2 Create housemate statistics shared contract types
    - Create `HousemateStatisticsResponse.cs`, `CookingShareDto.cs`, `TopDishDto.cs`, `HousemateTimelineDto.cs` in `Happie.Shared/Contracts/`
    - Each record uses `[JsonPropertyName]` attributes matching the wire format defined in design
    - _Requirements: 11.2, 11.3, 11.4_

  - [x] 1.3 Create API result types
    - Create `DishStatisticsResult.cs`, `DishTimelineEntry.cs`, `HousemateStatisticsResult.cs`, `CookingShareEntry.cs`, `TopDishEntry.cs`, `HousemateTimelineEntry.cs` in `Happie.Api/Results/`
    - These are internal handler return types mapped to contracts at the function layer
    - _Requirements: 11.1, 11.2_

- [x] 2. Implement DishStatisticsHandler
  - [x] 2.1 Create IDishStatisticsHandler interface and DishStatisticsHandler class
    - Create `IDishStatisticsHandler.cs` in `Happie.Api/Handlers/`
    - Create `DishStatisticsHandler.cs` in `Happie.Api/Handlers/`
    - Inject `IAttendanceRepository`, `IDayPlanDishLinkRepository`, `ISavedDishRepository`, `IHousemateRepository`
    - Implement statistics computation: times cooked (in-range and all-time), last cooked date, timeline entries per housemate
    - Filter out soft-deleted dishes, apply date range filtering, group chef days per housemate for timeline
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.2, 3.3, 3.4, 3.5, 9.1, 9.2_

  - [x] 2.2 Write property tests for DishStatisticsHandler — Property 1: Dish cooking day count
    - **Property 1: Dish cooking day count**
    - Generate random DayPlanDishLink and SavedDish data, verify times cooked equals distinct dates with non-deleted dish links
    - **Validates: Requirements 2.1, 2.4**

  - [x] 2.3 Write property tests for DishStatisticsHandler — Property 2: Last cooked date
    - **Property 2: Last cooked date**
    - Generate random cooking days, verify last cooked date equals the maximum date in the set
    - **Validates: Requirements 2.3**

  - [x] 2.4 Write property tests for DishStatisticsHandler — Property 3: Dish timeline dot correctness
    - **Property 3: Dish timeline dot correctness**
    - Generate random attendance and dish link data, verify each housemate's cooking days match IsChef=true AND DayPlanDishLink exists for the dish within the timeline window
    - **Validates: Requirements 3.2, 3.5**

  - [x] 2.5 Write property tests for DishStatisticsHandler — Property 4: Dish timeline sort order
    - **Property 4: Dish timeline sort order**
    - Generate random housemates with varying SortOrder, verify timeline rows are ordered by ascending SortOrder
    - **Validates: Requirements 3.3**

  - [x] 2.6 Write property tests for DishStatisticsHandler — Property 5: Color attribution correctness
    - **Property 5: Color attribution correctness**
    - Generate random housemates with assigned colors, verify timeline entry colors match housemate Color field exactly
    - **Validates: Requirements 3.4, 6.2**

  - [x] 2.7 Write property tests for DishStatisticsHandler — Property 16: Soft-delete exclusion
    - **Property 16: Soft-delete exclusion**
    - Generate random data with mix of deleted and non-deleted dishes, verify deleted dishes are excluded from all counts and timeline dots
    - **Validates: Requirements 9.1, 9.4**

- [x] 3. Implement HousemateStatisticsHandler
  - [x] 3.1 Create IHousemateStatisticsHandler interface and HousemateStatisticsHandler class
    - Create `IHousemateStatisticsHandler.cs` in `Happie.Api/Handlers/`
    - Create `HousemateStatisticsHandler.cs` in `Happie.Api/Handlers/`
    - Inject `IAttendanceRepository`, `IDayPlanDishLinkRepository`, `ISavedDishRepository`, `IHousemateRepository`
    - Implement: times cooked, days eating in, cook ratio, longest streak, busiest week, cooking shares, top dishes, timeline entries
    - Filter out soft-deleted dishes, apply date range filtering
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 7.1, 7.2, 7.3, 7.4, 8.2, 8.3, 8.4, 9.1, 9.2_

  - [x] 3.2 Write property test for HousemateStatisticsHandler — Property 6: Housemate chef day count
    - **Property 6: Housemate chef day count**
    - Generate random AttendanceRecords, verify times cooked equals distinct dates with IsChef=true within range
    - **Validates: Requirements 5.1**

  - [x] 3.3 Write property test for HousemateStatisticsHandler — Property 7: Days eating in count
    - **Property 7: Days eating in count**
    - Generate random AttendanceRecords, verify days eating in equals distinct dates with Status=EatingIn within range
    - **Validates: Requirements 5.3**

  - [x] 3.4 Write property test for HousemateStatisticsHandler — Property 8: Cook ratio computation
    - **Property 8: Cook ratio computation**
    - Generate random AttendanceRecords, verify X = days with IsChef AND EatingIn, Y = days with EatingIn within range
    - **Validates: Requirements 5.4**

  - [x] 3.5 Write property test for HousemateStatisticsHandler — Property 9: Longest streak computation
    - **Property 9: Longest streak computation**
    - Generate random sequences of chef days, verify longest streak equals length of longest consecutive run
    - **Validates: Requirements 5.5**

  - [x] 3.6 Write property test for HousemateStatisticsHandler — Property 10: Busiest week computation
    - **Property 10: Busiest week computation**
    - Generate random chef days, verify busiest week equals max chef days in any Monday-to-Sunday ISO week
    - **Validates: Requirements 5.6**

  - [x] 3.7 Write property test for HousemateStatisticsHandler — Property 11: Cooking share computation
    - **Property 11: Cooking share computation**
    - Generate random attendance data for multiple housemates, verify each housemate's chef-day count is correct and multi-chef days count independently
    - **Validates: Requirements 6.1, 6.5, 6.6**

  - [x] 3.8 Write property test for HousemateStatisticsHandler — Property 12: Cooking share percentage
    - **Property 12: Cooking share percentage**
    - Generate random cooking share entries with total > 0, verify percentage equals Math.Round(count / total * 100)
    - **Validates: Requirements 6.4**

  - [x] 3.9 Write property test for HousemateStatisticsHandler — Property 13: Top dishes computation
    - **Property 13: Top dishes computation**
    - Generate random data, verify top dishes list has at most 10 entries, sorted by count desc then alphabetically, with non-empty description and count > 0
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

  - [x] 3.10 Write property test for HousemateStatisticsHandler — Property 14: Housemate timeline dot correctness
    - **Property 14: Housemate timeline dot correctness**
    - Generate random data, verify each dish's cooking days match IsChef=true AND DayPlanDishLink exists for that dish within timeline window
    - **Validates: Requirements 8.2, 8.4**

  - [x] 3.11 Write property test for HousemateStatisticsHandler — Property 15: Housemate timeline sort order
    - **Property 15: Housemate timeline sort order**
    - Generate random data, verify dish rows ordered by all-time frequency descending with alphabetical description as tie-breaker
    - **Validates: Requirements 8.3**

- [x] 4. Checkpoint — Ensure all handler tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement StatisticsFunction (API endpoints)
  - [x] 5.1 Create StatisticsFunction with GetDishStatisticsAsync endpoint
    - Create `StatisticsFunction.cs` in `Happie.Api/Functions/`
    - Register route `GET /api/saved-dishes/{id}/statistics`
    - Parse `from`, `to`, `timelineFrom`, `timelineTo` query parameters
    - Validate GUID format, date formats, from<=to constraints
    - Return 400 for invalid params, 404 for non-existent/deleted dish
    - Delegate to `IDishStatisticsHandler`, map result to `DishStatisticsResponse`
    - _Requirements: 11.1, 11.3, 11.4, 11.5, 11.6, 11.7, 11.9, 11.10_

  - [x] 5.2 Add GetHousemateStatisticsAsync endpoint to StatisticsFunction
    - Register route `GET /api/housemates/{id}/statistics`
    - Parse same query parameters as dish endpoint
    - Validate GUID format, date formats, from<=to constraints
    - Return 400 for invalid params, 404 for non-existent housemate
    - Delegate to `IHousemateStatisticsHandler`, map result to `HousemateStatisticsResponse`
    - _Requirements: 11.2, 11.3, 11.4, 11.5, 11.6, 11.8, 11.9, 11.10_

  - [x] 5.3 Register handlers in DI container
    - Add `IDishStatisticsHandler`/`DishStatisticsHandler` and `IHousemateStatisticsHandler`/`HousemateStatisticsHandler` to the service registration in `Program.cs`
    - _Requirements: 11.1, 11.2_

  - [x] 5.4 Write unit tests for StatisticsFunction
    - Test route/query parameter validation: missing dates, invalid date format, from > to, invalid GUID, non-existent resource
    - Test correct delegation to handlers and response mapping
    - _Requirements: 11.5, 11.6, 11.7, 11.8, 11.9_

- [x] 6. Implement client-side StatisticsApiClient
  - [x] 6.1 Create StatisticsApiClient service
    - Create `StatisticsApiClient.cs` in `Happie.Web/Http/` (or `Services/` based on existing pattern)
    - Add methods: `GetDishStatisticsAsync(Guid dishId, DateOnly from, DateOnly to, DateOnly timelineFrom, DateOnly timelineTo)` and `GetHousemateStatisticsAsync(Guid housemateId, DateOnly from, DateOnly to, DateOnly timelineFrom, DateOnly timelineTo)`
    - Handle HTTP errors: 404 → return null (caller redirects), 400 → throw, 401 → handled by existing interceptor
    - Register in DI
    - _Requirements: 11.1, 11.2, 1.8, 4.11_

- [x] 7. Implement reusable UI components
  - [x] 7.1 Create TimeRangeSelector component
    - Create `TimeRangeSelector.razor` and `TimeRangeSelector.razor.css` in `Happie.Web/Components/`
    - Define `TimeRange` enum (ThirtyDays, ThreeMonths, OneYear, AllTime) in `Happie.Web/`
    - Render four pill buttons with 44x44px min touch target
    - Accept `SelectedRange` parameter and `OnRangeSelected` EventCallback
    - Visually distinguish active pill
    - _Requirements: 1.5, 1.6, 1.7, 4.8, 4.9, 4.10, 13.2_

  - [x] 7.2 Create TimelineChart component
    - Create `TimelineChart.razor` and `TimelineChart.razor.css` in `Happie.Web/Components/`
    - Render SVG/HTML dot grid with entities on Y-axis and days on X-axis
    - Support horizontal CSS scroll with touch-swipe
    - Implement infinite scroll-back: detect left-edge scroll position, emit `OnLoadMore` callback
    - Pre-load 3 months on initial render
    - Show empty state when no data
    - _Requirements: 3.1, 3.6, 3.7, 3.8, 3.9, 3.10, 8.1, 8.5, 8.6, 8.7, 13.3_

  - [x] 7.3 Create DonutChart component
    - Create `DonutChart.razor` and `DonutChart.razor.css` in `Happie.Web/Components/`
    - Render SVG donut chart with color segments per housemate
    - Visually distinguish the current housemate's segment (offset or thicker stroke)
    - Display rounded percentage labels
    - Fit within viewport width without horizontal scroll
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.7, 13.5_

  - [x] 7.4 Create OverflowMenu component for HousematesPage
    - Create `OverflowMenu.razor` and `OverflowMenu.razor.css` in `Happie.Web/Components/`
    - Three horizontal dots (⋯) trigger button
    - Dropdown with Move Up, Move Down, Delete actions
    - Dynamically hide Move Up for first row, Move Down for last row
    - Close on outside click
    - _Requirements: 4.1, 4.2, 4.3, 4.6_

- [x] 8. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement DishDetailsPage
  - [x] 9.1 Create DishDetailsPage with routing and data loading
    - Create `DishDetailsPage.razor` and `DishDetailsPage.razor.css` in `Happie.Web/Pages/`
    - Route: `/saved-dishes/{id}`
    - On load: validate GUID, call StatisticsApiClient, redirect to SavedDishesPage on 404
    - Display dish description as heading
    - Integrate TimeRangeSelector (default 30 days)
    - Show loading indicator during fetch
    - _Requirements: 1.2, 1.4, 1.5, 1.6, 1.7, 1.8_

  - [x] 9.2 Implement dish summary statistics display
    - Display primary "times cooked" count at larger font size
    - Display all-time count in secondary style below
    - Display "last cooked" indicator: relative format ("X days ago"/"today") if within 30 days, otherwise locale-formatted absolute date
    - Handle zero-data states (hide last cooked when zero all-time, show 0 for times cooked)
    - _Requirements: 2.1, 2.2, 2.3, 2.5, 2.6, 10.1_

  - [x] 9.3 Integrate TimelineChart on DishDetailsPage
    - Wire TimelineChart with housemate rows, pass cooking day data
    - Implement scroll-back: call StatisticsApiClient with adjusted timelineFrom/timelineTo
    - Handle empty state (no cooking history message)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 3.10, 10.3, 10.5_

  - [x] 9.4 Add statistics icon button to SavedDishesPage
    - Add a statistics (chart) icon button alongside existing edit and delete buttons on each dish row
    - Tapping navigates to `/saved-dishes/{id}`
    - Existing edit/delete buttons remain unchanged and do not navigate to DishDetailsPage
    - _Requirements: 1.1, 1.3_

- [x] 10. Implement HousemateDetailsPage
  - [x] 10.1 Create HousemateDetailsPage with routing and data loading
    - Create `HousemateDetailsPage.razor` and `HousemateDetailsPage.razor.css` in `Happie.Web/Pages/`
    - Route: `/housemates/{id}`
    - On load: validate GUID, call StatisticsApiClient, redirect to HousematesPage on 404
    - Display housemate name as heading
    - Integrate TimeRangeSelector (default 30 days)
    - _Requirements: 4.4, 4.7, 4.8, 4.9, 4.10, 4.11_

  - [x] 10.2 Implement housemate summary statistics display
    - Display primary "times cooked" count (larger font)
    - Display all-time count in secondary style
    - Display "days eating in" count
    - Display cook ratio as "Cooked X of Y eating-in days"
    - Display longest streak with flame icon
    - Display busiest week statistic
    - Handle zero-data state (all zeros, hide sections)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 10.2_

  - [x] 10.3 Integrate DonutChart on HousemateDetailsPage
    - Wire DonutChart with cooking share data
    - Pass current housemate ID for visual distinction
    - Hide donut chart when no chef days in range
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.7_

  - [x] 10.4 Implement top dishes section on HousemateDetailsPage
    - Render top dishes list (max 10), sorted by frequency desc, alphabetical tie-breaker
    - Display dish description and count per entry
    - Hide section when housemate has no cooking days in range
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 10.5 Integrate TimelineChart on HousemateDetailsPage
    - Wire TimelineChart with dish rows sorted by all-time frequency desc
    - Implement scroll-back with 1-month chunks
    - Handle empty state
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 10.3, 10.5_

  - [x] 10.6 Refactor HousematesPage to use OverflowMenu and add statistics button
    - Replace existing reorder/delete buttons with new layout: rename, color, statistics, overflow (⋯)
    - Statistics button navigates to `/housemates/{id}`
    - Overflow menu contains Move Up/Move Down/Delete (contextual visibility)
    - Ensure tap-to-switch-active behavior remains on the row itself
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

- [x] 11. Handle empty states and edge cases
  - [x] 11.1 Implement empty state messaging
    - Add localized empty state strings to `SharedStrings.resx` and `SharedStrings.en.resx`
    - DishDetailsPage: hide summary stats, show empty message when zero cooking days in range
    - HousemateDetailsPage: hide summary stats, donut chart, top dishes, show empty message when zero chef days in range
    - TimelineChart: show empty state message in place of chart when no data points
    - Independent behavior: timeline can be empty while summary has data (and vice versa)
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [x] 12. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. Final wiring and cleanup
  - [x] 13.1 Verify end-to-end page layout and mobile styling
    - Ensure DishDetailsPage and HousemateDetailsPage use same layout/padding/font conventions as existing pages
    - Verify single-column vertical stack layout
    - Verify summary stats use larger font size as focal points
    - Verify Time_Range_Selector pills have 44x44px min touch target
    - _Requirements: 13.1, 13.2, 13.4, 13.6_

  - [x] 13.2 Delete feature prompt file
    - Delete `.kiro/specs/feature-prompts/prompt-statistics.md` from the repository
    - _Requirements: 12.1_

  - [x] 13.3 Write integration tests for Statistics API
    - Test end-to-end flow against Azurite with seeded data
    - Verify response shape, soft-delete exclusion, date range filtering
    - _Requirements: 11.1, 11.2, 9.1_

  - [x] 13.4 Write bUnit component tests
    - Test TimeRangeSelector rendering, pill selection state
    - Test DishDetailsPage and HousemateDetailsPage empty state display and navigation guards
    - Test DonutChart segment rendering
    - _Requirements: 1.5, 1.6, 1.8, 4.9, 4.11, 10.1, 10.2_

- [x] 14. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# throughout (Blazor WebAssembly + Azure Functions), consistent with the existing codebase
- All new types follow one-type-per-file convention per project coding standards

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "3.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6", "2.7", "3.2", "3.3", "3.4", "3.5", "3.6", "3.7", "3.8", "3.9", "3.10", "3.11"] },
    { "id": 3, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 4, "tasks": ["5.4", "6.1"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3", "7.4"] },
    { "id": 6, "tasks": ["9.1", "9.4", "10.1", "10.6"] },
    { "id": 7, "tasks": ["9.2", "9.3", "10.2", "10.3", "10.4", "10.5"] },
    { "id": 8, "tasks": ["11.1"] },
    { "id": 9, "tasks": ["13.1", "13.2", "13.3", "13.4"] }
  ]
}
```
