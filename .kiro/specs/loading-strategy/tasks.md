# Implementation Plan: Loading Strategy

## Overview

This implementation formalizes the loading indicator strategy for the Happie PWA by creating a reusable `LoadingDots` component, fixing inconsistent loading states in existing pages (HousematesPage, DishDetailsPage sub-panels), and documenting the rules in a steering file. The work is organized into: component creation, page fixes, steering documentation, and testing.

## Tasks

- [x] 1. Create the LoadingDots component
  - [x] 1.1 Create `LoadingDots.razor` and `LoadingDots.razor.css`
    - Create `Happie.Web/Components/LoadingDots.razor` with three dot spans, `role="status"`, and `aria-label` from `LoadingIndicator_AriaLabel` localization key
    - Create `Happie.Web/Components/LoadingDots.razor.css` with flexbox layout, 8px dots, bounce animation (1.2s cycle with staggered delays), and `prefers-reduced-motion` fallback using opacity fade
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

  - [x] 1.2 Write property test for LoadingDots accessibility attributes
    - **Property 3: LoadingDots Accessibility Attributes**
    - **Validates: Requirements 5.7**
    - Create `Happie.Web.Tests/Components/LoadingDotsPropertyTests.cs`
    - Render `LoadingDots` with various localizer configurations and assert `role="status"` is always present and `aria-label` is always non-null and non-empty

  - [x] 1.3 Write bUnit tests for LoadingDots component
    - Create `Happie.Web.Tests/Components/LoadingDotsTests.cs`
    - Test that three `.loading-dots__dot` elements are rendered
    - Test that root element has `role="status"`
    - Test that `aria-label` attribute is present and non-empty
    - _Requirements: 5.7_

- [x] 2. Fix HousematesPage loading consistency
  - [x] 2.1 Replace plain `<p>` loading text with `<LoadingDots>` in HousematesPage
    - In `Happie.Web/Pages/HousematesPage.razor`, replace the `<p>@Localizer["DayPlan_Loading"]</p>` block with a `<div class="loading-delayed"><LoadingDots /></div>` wrapper
    - _Requirements: 3.1, 3.2_

  - [x] 2.2 Write bUnit tests for HousematesPage loading state
    - Add tests to `Happie.Web.Tests/Pages/` verifying that while loading, `LoadingDots` is rendered inside a `.loading-delayed` container
    - Verify the old plain text loading message is no longer rendered
    - _Requirements: 3.1, 3.2_

- [x] 3. Add loading state to DishDetailsPage sub-panels
  - [x] 3.1 Add `_isLoading` tracking to SummaryPanel
    - In `Happie.Web/Components/SummaryPanel.razor`, add `_isLoading = true` field
    - Wrap `LoadDataAsync` with `_isLoading = true` before fetch and `_isLoading = false` in `finally`
    - Show `<LoadingDots>` inside a `.loading-delayed` container when `_isLoading` is true
    - Show empty state only when `_isLoading` is false and data is empty
    - Hide the edit button while `_isLoading` is true
    - _Requirements: 2.1, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 3.2 Add `_isLoading` tracking to IngredientsPanel
    - In `Happie.Web/Components/IngredientsPanel.razor`, add `_isLoading = true` field
    - Wrap fetch logic with `_isLoading = true` before fetch and `_isLoading = false` in `finally`
    - Show `<LoadingDots>` inside a `.loading-delayed` container when `_isLoading` is true
    - Show empty state only when `_isLoading` is false and data is empty
    - Hide the edit button while `_isLoading` is true
    - _Requirements: 2.2, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 3.3 Add `_isLoading` tracking to InstructionsPanel
    - In `Happie.Web/Components/InstructionsPanel.razor`, add `_isLoading = true` field
    - Wrap fetch logic with `_isLoading = true` before fetch and `_isLoading = false` in `finally`
    - Show `<LoadingDots>` inside a `.loading-delayed` container when `_isLoading` is true
    - Show empty state only when `_isLoading` is false and data is empty
    - Hide the edit button while `_isLoading` is true
    - _Requirements: 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 3.4 Write bUnit tests for SummaryPanel loading state
    - Update `Happie.Web.Tests/Components/SummaryPanelTests.cs` with tests for:
      - While fetching: renders `LoadingDots`, does not render empty state, does not render edit button
      - After fetch success (data): renders content, no `LoadingDots`
      - After fetch success (empty): renders empty state message, no `LoadingDots`
      - After fetch failure: renders empty state message, no `LoadingDots`
      - Loading container has `.loading-delayed` CSS class
    - _Requirements: 2.1, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 3.5 Write bUnit tests for IngredientsPanel loading state
    - Update `Happie.Web.Tests/Components/IngredientsPanelTests.cs` with the same test matrix as SummaryPanel
    - _Requirements: 2.2, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 3.6 Write bUnit tests for InstructionsPanel loading state
    - Update `Happie.Web.Tests/Components/InstructionsPanelTests.cs` with the same test matrix as SummaryPanel
    - _Requirements: 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Write property tests for loading category logic
  - [x] 5.1 Write property test for loading category mutual exclusivity
    - **Property 1: Loading Category Mutual Exclusivity**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**
    - Create `Happie.Web.Tests/Components/LoadingCategoryPropertyTests.cs`
    - Model category selection as a pure function taking a `LoadingScenario` record (`HasCachedData`, `IsBackgroundRefresh`, `IsSubPanel`, `IsFilterChange`)
    - Generate random valid scenarios with FsCheck and assert exactly one category is returned
    - Assert Panel_Loading_Indicator is returned when `IsSubPanel` is true

  - [x] 5.2 Write property test for loading state transitions
    - **Property 2: Loading State Transition on Fetch Completion**
    - **Validates: Requirements 1.4, 1.5, 1.6, 2.4, 2.5, 2.7**
    - Add to `Happie.Web.Tests/Components/LoadingCategoryPropertyTests.cs`
    - Model state machine as `(LoadingState, FetchOutcome) → LoadingState`
    - Generate random initial states and fetch outcomes (SuccessWithData, SuccessEmpty, Failure)
    - Assert resulting state always has `IsLoading = false`

- [x] 6. Create the loading strategy steering document
  - [x] 6.1 Create `.kiro/steering/loading-strategy.md`
    - Add YAML frontmatter with `inclusion: fileMatch` and `fileMatchPattern: "**/*.razor,**/*.razor.css,**/Happie.Web/**"`
    - Write sections for each loading category: Header_Spinner, Inline_Loading_Dots, Stale_Overlay, Panel_Loading_Indicator
    - Include Blazor code examples for each category
    - Explain the role of `LoadingIndicator`, `LoadingIndicatorState`, `LoadingDots`, and `.loading-delayed`
    - Include correct/incorrect usage examples for empty state vs loading state
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 6.2 Update `project-context.md` steering file index
    - Add a row for `loading-strategy.md` in the steering file index table with Topic: "Loading indicators and loading state conventions" and Loaded when: "Editing `.razor` / `Happie.Web/`"
    - _Requirements: 4.5_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests (bUnit) validate specific rendering examples and edge cases
- The `LoadingDots` component is intentionally parameterless — positioning is handled by the parent container
- The existing `.loading-delayed` CSS class is reused unchanged (200ms fade-in delay)
- No backend or data model changes are needed — this feature is purely presentational

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.1", "3.1", "3.2", "3.3"] },
    { "id": 2, "tasks": ["2.2", "3.4", "3.5", "3.6", "5.1", "5.2"] },
    { "id": 3, "tasks": ["6.1"] },
    { "id": 4, "tasks": ["6.2"] }
  ]
}
```
