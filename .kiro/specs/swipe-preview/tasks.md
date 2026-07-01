# Implementation Plan: Swipe Preview

## Overview

This plan implements a three-panel swipe carousel on `DayPlanPage` that shows live content of the adjacent day sliding into view during horizontal swipe gestures. The implementation spans the Blazor WASM component (carousel state, panel rendering, pre-fetching) and a new JS interop handler (`happie.registerSwipeCarousel`) that manages touch tracking, direction lock, rubber-band physics, and completion/snap-back animations. Tasks are ordered so the pure math utilities and JS handler are built first, then the Blazor carousel structure, then pre-fetching and panel recycling, and finally integration wiring.

## Tasks

- [x] 1. Create SwipeCarouselMath utility and property tests
  - [x] 1.1 Create `SwipeCarouselMath.cs` static utility class
    - Create `Happie.Web/Services/SwipeCarouselMath.cs`
    - Implement `RubberBand(double dragDistance, double viewportWidth)` — linear up to viewport width, then diminishing returns capped at 1.2× viewport width
    - Implement `IsInEdgeExclusionZone(double clientX, double viewportWidth)` — returns true if clientX ≤ 20 or clientX ≥ viewportWidth − 20
    - Implement `DetermineDirectionLock(double deltaX, double deltaY)` — returns `true` if horizontal locked, `false` if vertical locked, `null` if undetermined (neither axis exceeds 10px)
    - Implement `ShouldNavigate(double dragDistance)` — returns true if |dragDistance| ≥ 60px
    - _Requirements: 2.1, 2.4, 2.5, 2.6, 3.1, 4.1, 9.1_

  - [ ]* 1.2 Write property test: Linear translation matches drag distance
    - **Property 1: Linear translation matches drag distance**
    - For drag distances in [0, viewportWidth], verify RubberBand output equals the input drag distance
    - **Validates: Requirements 2.1**

  - [ ]* 1.3 Write property test: Rubber-band never exceeds 1.2× viewport width
    - **Property 2: Rubber-band never exceeds 1.2× viewport width**
    - Generate random drag distances (up to 10× viewport width) and viewport widths in [320, 1920], verify |RubberBand(D, W)| ≤ 1.2 × W
    - **Validates: Requirements 2.4**

  - [ ]* 1.4 Write property test: Direction lock decision consistent with first axis to exceed 10px
    - **Property 3: Direction lock decision is consistent with first axis to exceed 10px**
    - Generate random (deltaX, deltaY) pairs, verify lock decision matches which axis exceeds 10px first
    - **Validates: Requirements 2.5, 2.6**

  - [ ]* 1.5 Write property test: Swipe outcome determined by threshold comparison
    - **Property 4: Swipe outcome determined by threshold comparison**
    - Generate random drag distances in [0, 300], verify ≥ 60 produces navigate and < 60 produces snap-back
    - **Validates: Requirements 3.1, 4.1**

  - [ ]* 1.6 Write property test: Edge exclusion rejects touches within 20px of viewport edges
    - **Property 6: Edge exclusion rejects touches within 20px of viewport edges**
    - Generate random (clientX, viewportWidth) pairs with viewportWidth in [320, 1920], verify accept/reject matches the 20px edge rule
    - **Validates: Requirements 9.1**

- [x] 2. Implement JS swipe carousel handler
  - [x] 2.1 Create `happie.registerSwipeCarousel` in `wwwroot/js/swipeCarousel.js`
    - Implement touch event listeners (touchstart, touchmove, touchend, touchcancel) on the carousel wrapper
    - Implement edge exclusion zone check (clientX ≤ 20 or clientX ≥ viewport width − 20) at touch start
    - Implement input element exclusion (input, textarea, select, contenteditable, role="dialog") at touch start
    - Implement 10px direction lock dead zone — lock horizontal or allow vertical scroll
    - Implement per-frame translateX on the carousel wrapper via requestAnimationFrame
    - Implement rubber-band resistance when drag exceeds viewport width (diminishing to max 1.2×)
    - Implement swipe completion animation (slide full viewport width, ≤ 300ms) when threshold (60px) is met
    - Implement snap-back animation (return to 0, ≤ 300ms) when below threshold
    - Implement snap-back interruption — new touch cancels snap-back, resumes from current position
    - Block new swipe gestures during completion animation
    - Call .NET `SwipeLeftAsync` or `SwipeRightAsync` via DotNetObjectReference on threshold completion
    - Register the script in `index.html`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 9.1, 9.2, 9.3, 9.4, 10.2_

  - [x] 2.2 Create `happie.disposeSwipeCarousel` cleanup method
    - Remove all event listeners registered by `registerSwipeCarousel`
    - Cancel any in-progress animation
    - Called from Blazor `Dispose` to prevent memory leaks
    - _Requirements: 10.2_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement carousel structure in DayPlanPage
  - [x] 4.1 Add carousel wrapper markup and CSS to `DayPlanPage.razor`
    - Add `<div class="swipe-carousel">` wrapper containing three panel divs (previous, active, next)
    - Position panels with CSS: previous at `left: -100%`, active at `left: 0`, next at `left: 100%`
    - Carousel wrapper: `overflow: hidden`, full viewport width, `will-change: transform` during touch
    - Panels: `width: 100%`, `position: absolute`, full height
    - Active panel occupies full viewport with adjacent panels entirely off-screen
    - _Requirements: 1.1, 1.2_

  - [x] 4.2 Add carousel state fields to `DayPlanPage.razor.cs`
    - Add `_prevDayPlan`, `_nextDayPlan` (DayPlanResponse?) fields
    - Add `_prevLoaded`, `_nextLoaded` (bool) flags
    - Add `_isSwiping` (bool) flag to defer state updates during swipe
    - Add `_prefetchCts` (CancellationTokenSource?) for pre-fetch cancellation
    - Add `_prevDate`, `_nextDate` (DateOnly) computed from `_parsedDate`
    - _Requirements: 1.1, 5.4_

  - [x] 4.3 Implement `RenderDayPlanContent` method
    - Create private method that renders panel content: full DayPlanPage content (DateNavigationPanel, DishPanel, AttendanceSection, etc.) when data is loaded, or Arrow_Placeholder when not loaded
    - Arrow placeholder shows left arrow for previous panel, right arrow for next panel
    - Reuse existing arrow SVG style
    - _Requirements: 1.3, 1.4, 7.1_

  - [x] 4.4 Wire JS interop for `registerSwipeCarousel` on page load
    - Call `happie.registerSwipeCarousel` after first render via `OnAfterRenderAsync`
    - Pass DotNetObjectReference for `SwipeLeftAsync` / `SwipeRightAsync` callbacks
    - Implement `[JSInvokable] SwipeLeftAsync` — triggers navigation to next day
    - Implement `[JSInvokable] SwipeRightAsync` — triggers navigation to previous day
    - Dispose JS handler on page teardown
    - _Requirements: 3.3, 3.4_

- [x] 5. Implement prioritized pre-fetching
  - [x] 5.1 Implement `PreFetchAdjacentDaysAsync` method
    - Call after active day data has loaded and rendered (after first `StateHasChanged` cycle)
    - Fetch previous day and next day data via `ICachedApiClient.GetDayPlanAsync` asynchronously (fire-and-forget, no await blocking UI)
    - On success: set `_prevDayPlan` / `_nextDayPlan` and `_prevLoaded` / `_nextLoaded` flags, call `StateHasChanged`
    - On failure: leave Arrow_Placeholder in place, do not show loading indicator
    - Use `_prefetchCts` cancellation token for all pre-fetch calls
    - Do NOT show `LoadingIndicatorState` for pre-fetch operations
    - _Requirements: 5.1, 5.2, 5.3, 6.1, 6.2, 6.5_

  - [x] 5.2 Implement pre-fetch cancellation on navigation
    - In `OnParametersSetAsync`: cancel `_prefetchCts` when date parameter changes
    - Create new `_prefetchCts` for the new date
    - Discard results of outdated pre-fetches (cancellation token prevents stale assignment)
    - _Requirements: 5.4_

  - [ ]* 5.3 Write property test: Pre-fetch results discarded on navigation away
    - **Property 5: Pre-fetch results discarded on navigation away**
    - Simulate rapid date navigations with pending pre-fetches, verify only the final date's adjacent data is applied
    - **Validates: Requirements 5.4**

- [x] 6. Implement panel recycling after navigation
  - [x] 6.1 Implement panel role reassignment in `OnParametersSetAsync`
    - After swipe navigation: reassign panel roles so the newly navigated day is the Active_Panel
    - The former Active_Panel becomes the Adjacent_Panel on the opposite side
    - The panel two days away from new current date releases content and reverts to Arrow_Placeholder
    - Initiate pre-fetch for the new adjacent day that is not already loaded
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 6.2 Implement sequential navigation guard
    - Ensure each navigation completes before accepting the next swipe gesture
    - Use the `_isSwiping` flag and JS animation blocking to prevent rapid concurrent navigations
    - Panel roles must be consistent before next swipe is accepted
    - _Requirements: 8.5_

- [ ]* 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement swipe completion and cancellation behavior
  - [x] 8.1 Implement deferred state updates during swipe
    - When `_isSwiping` is true: queue incoming pre-fetch data updates
    - When swipe completes or is cancelled: apply queued updates and call `StateHasChanged`
    - Ensure arrow placeholder is not replaced mid-swipe (defer until gesture ends)
    - _Requirements: 7.4_

  - [x] 8.2 Implement fallback navigation for unloaded adjacent panels
    - When swipe completes toward an Adjacent_Panel showing Arrow_Placeholder: navigate to `/day/{date}` via `NavigationManager.NavigateTo`
    - Navigation still functions even when pre-fetch failed
    - _Requirements: 7.2, 6.4_

- [x] 9. Implement touch exclusion zones and performance safeguards
  - [~] 9.1 Verify edge exclusion and input element exclusion in JS handler
    - Ensure touches within 20px of left/right viewport edges are ignored
    - Ensure touches on input, textarea, select, contenteditable, role="dialog" elements are ignored
    - Exclusion evaluated only at touch start — subsequent movement does not change the determination
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [~] 9.2 Implement deferred adjacent panel rendering for performance
    - Render Active_Panel content to DOM before initiating Adjacent_Panel rendering
    - If Adjacent_Panel rendering causes frame drops (>16ms) or input lag (>100ms): defer remaining rendering until 200ms of idle time
    - Use `requestIdleCallback` or equivalent scheduling to avoid blocking the main thread
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 10. Remove old swipe handler and wire up carousel
  - [x] 10.1 Replace `happie.registerSwipe` with `happie.registerSwipeCarousel` on DayPlanPage
    - Remove the old `happie.registerSwipe` call from DayPlanPage
    - Remove the old swipe arrow indicators (now rendered inside adjacent panels)
    - Ensure all existing swipe navigation behavior (threshold, direction) is preserved by the new carousel handler
    - Keep `happie.registerSwipe` available for other pages if used elsewhere
    - _Requirements: 2.1, 3.3, 3.4_

  - [ ]* 10.2 Write unit tests for carousel state management
    - Test panel state transitions on navigation (recycling)
    - Test pre-fetch ordering (current day before ±1)
    - Test pre-fetch cancellation on date change
    - Test deferred state updates during swipe
    - Test arrow placeholder direction (left panel → left arrow, right panel → right arrow)
    - Test fallback navigation when adjacent data not loaded
    - _Requirements: 1.1, 5.1, 5.4, 7.1, 7.2, 8.1, 8.2, 8.3_

- [~] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck (minimum 100 iterations)
- Unit tests validate specific examples and edge cases using xUnit
- The JS handler (`swipeCarousel.js`) owns all touch tracking and animation for 60fps performance; Blazor manages state and data
- The carousel is a progressive enhancement — if JS interop fails, navigation via DateNavigationPanel arrows still works
- No server-side changes are required; pre-fetching uses the existing `ICachedApiClient.GetDayPlanAsync` stale-while-revalidate mechanism
- Properties 1–4 and 6 test pure C# functions in `SwipeCarouselMath`; Property 5 tests async cancellation logic in the Blazor component

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4", "1.5", "1.6", "2.2"] },
    { "id": 2, "tasks": ["4.1", "4.2"] },
    { "id": 3, "tasks": ["4.3", "4.4"] },
    { "id": 4, "tasks": ["5.1", "5.2"] },
    { "id": 5, "tasks": ["5.3", "6.1"] },
    { "id": 6, "tasks": ["6.2", "8.1"] },
    { "id": 7, "tasks": ["8.2", "9.1", "9.2"] },
    { "id": 8, "tasks": ["10.1", "10.2"] }
  ]
}
```
