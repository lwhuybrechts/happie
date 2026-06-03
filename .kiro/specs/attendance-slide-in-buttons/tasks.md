# Implementation Plan: Attendance Slide-In Buttons

## Overview

Transform the `AttendanceSection` component so that each housemate row displays only the active attendance button in a compact collapsed state on narrow viewports (<480px). Tapping the active button triggers a 250ms slide animation revealing all three options. The row collapses back after status selection, focus loss, re-tap, auto-collapse timeout, or hover-leave (pointer devices). On wide viewports (≥480px), all buttons are always visible. Implementation is purely frontend — no API or data model changes.

## Tasks

- [x] 1. Extract state management into testable service class
  - [x] 1.1 Create `AttendanceRowStateManager` class in `Happie.Web/Services/`
    - Encapsulate expand/collapse state: `_expandedHousemateId`, `_animatingIds`, `_autoCollapseTimer`, `_isNarrowViewport`, `_hasPointerDevice`, `_expandedViaHover`
    - Implement `ExpandAsync(Guid housemateId)` — sets expanded ID, enforces single-row policy, starts auto-collapse timer
    - Implement `CollapseAsync(Guid housemateId)` — clears expanded ID, cancels timer
    - Implement `HandleActiveButtonClickAsync(Guid housemateId)` — expands if collapsed
    - Implement `HandleExpandedButtonClickAsync(Guid housemateId, AttendanceStatus newStatus)` — collapses, returns whether status changed
    - Implement `HandleMouseEnterAsync(Guid housemateId)` — expands via hover when `_hasPointerDevice && _isNarrowViewport`
    - Implement `HandleMouseLeaveAsync(Guid housemateId)` — collapses if `_expandedViaHover`
    - Implement `HandleOutsideClickAsync()` — collapses current expanded row
    - Implement `HandleViewportChangeAsync(bool isNarrow)` — updates mode, collapses all when narrowing
    - Implement `IsExpanded(Guid)`, `IsAnimating(Guid)`, `IsCollapseEnabled` query methods
    - Implement `GetActiveStatus(Guid housemateId, AttendanceStatus currentStatus)` — returns the active button status
    - Implement animation lock: add to `_animatingIds` on expand/collapse, clear after 250ms
    - Implement auto-collapse timer: 3-second `System.Timers.Timer`, reset on new expand, cancel on click/collapse
    - Expose `StateChanged` event for component re-render notification
    - _Requirements: 1.1, 2.1, 2.6, 3.1, 4.1, 5.1, 5.3, 6.2, 6.3, 7.4, 10.1, 10.2, 10.3, 10.4, 11.1, 11.4, 12.1, 12.2, 12.3, 12.4_

  - [x] 1.2 Write property test: Active button matches current attendance status
    - **Property 1: Active button matches current attendance status**
    - **Validates: Requirements 1.1, 1.4**

  - [x] 1.3 Write property test: Expand on active button click when collapsed
    - **Property 2: Expand on active button click when collapsed**
    - **Validates: Requirements 2.1**

  - [x] 1.4 Write property test: Animation lock prevents all interaction
    - **Property 3: Animation lock prevents all interaction**
    - **Validates: Requirements 2.6, 5.3, 7.4**

  - [x] 1.5 Write property test: Status change collapses and applies new status
    - **Property 4: Status change collapses and applies new status optimistically**
    - **Validates: Requirements 3.1, 3.2**

  - [x] 1.6 Write property test: API failure reverts to previous status
    - **Property 5: API failure reverts to previous status**
    - **Validates: Requirements 3.5**

  - [x] 1.7 Write property test: Collapse without status change preserves status
    - **Property 6: Collapse without status change preserves status and sends no API request**
    - **Validates: Requirements 4.2, 5.1, 5.2**

  - [x] 1.8 Write property test: Single row expansion policy
    - **Property 7: Single row expansion policy**
    - **Validates: Requirements 6.2, 6.3**

  - [x] 1.9 Write property test: Auto-collapse timer lifecycle
    - **Property 9: Auto-collapse timer lifecycle matches row state**
    - **Validates: Requirements 10.1, 10.2, 10.3, 10.4**

  - [x] 1.10 Write property test: Wide viewport disables all collapse behavior
    - **Property 10: Wide viewport disables all collapse behavior**
    - **Validates: Requirements 11.1, 11.2, 11.5**

  - [x] 1.11 Write property test: Viewport narrowing collapses all rows
    - **Property 11: Viewport narrowing collapses all rows**
    - **Validates: Requirements 11.4**

  - [x] 1.12 Write property test: Hover expands row on pointer devices in narrow viewport
    - **Property 12: Hover expands row on pointer devices in narrow viewport**
    - **Validates: Requirements 12.1**

  - [x] 1.13 Write property test: Mouse-leave collapses hover-expanded row
    - **Property 13: Mouse-leave collapses hover-expanded row**
    - **Validates: Requirements 12.2**

  - [x] 1.14 Write property test: Hover expansion defers auto-collapse timer
    - **Property 14: Hover expansion defers auto-collapse timer**
    - **Validates: Requirements 12.3**

  - [x] 1.15 Write property test: Click during hover-expansion processes status change
    - **Property 15: Click during hover-expansion processes status change and collapses**
    - **Validates: Requirements 12.4**

- [x] 2. Checkpoint - Ensure state manager and property tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Add JS interop functions to `index.html`
  - [x] 3.1 Add viewport and hover detection JS interop functions to `Happie.Web/wwwroot/index.html`
    - Add `happie.registerViewportListener(dotNetRef)` — registers `matchMedia('(max-width: 479.98px)')` change listener, invokes `HandleViewportChangeAsync` on the .NET reference, returns initial `matches` value
    - Add `happie.unregisterViewportListener()` — removes the matchMedia listener
    - Add `happie.hasPointerDevice()` — returns `window.matchMedia('(hover: hover)').matches`
    - Add `happie.registerOutsideClick(dotNetRef, elementId)` — registers document-level click listener that invokes `HandleOutsideClickAsync` when click is outside the specified element
    - Add `happie.unregisterOutsideClick()` — removes the document click listener
    - _Requirements: 4.1, 11.1, 11.4, 12.1_

- [x] 4. Update `AttendanceSection.razor` component markup
  - [x] 4.1 Modify the `AttendanceSection.razor` component to integrate collapse/expand behavior
    - Inject `IJSRuntime` for JS interop calls
    - Add `_dotNetRef` (`DotNetObjectReference<AttendanceSection>`) for JS callbacks
    - Add `_sectionRef` (`ElementReference`) for outside-click detection
    - Instantiate `AttendanceRowStateManager` in `OnInitialized`
    - In `OnAfterRenderAsync(firstRender: true)`: call `happie.registerViewportListener`, `happie.hasPointerDevice`, configure state manager with results
    - Add `[JSInvokable]` methods `HandleViewportChangeAsync` and `HandleOutsideClickAsync` that delegate to state manager
    - In `Dispose`: call `happie.unregisterViewportListener`, `happie.unregisterOutsideClick`, dispose `_dotNetRef` and state manager
    - Update the `@foreach` loop to wrap attendance buttons in a `div.attendance-section__btn-group` container
    - Apply `--collapsed` or `--expanded` CSS class to the button group based on `IsExpanded(housemateId)`
    - Apply `attendance-section__btn--active` class to the button matching current status
    - Set `aria-expanded` on the active button based on expand state
    - Set `aria-hidden="true"` and `tabindex="-1"` on inactive buttons when collapsed
    - Remove `aria-hidden` and `tabindex` from inactive buttons when expanded
    - Add `@onmouseenter` and `@onmouseleave` event handlers (conditionally when `_hasPointerDevice && _isNarrowViewport`)
    - Wire active button click to `HandleActiveButtonClickAsync` (expand) vs `HandleExpandedButtonClickAsync` (status change or re-tap collapse)
    - Apply `attendance-section__name--truncated` class to name element when row is expanded
    - Apply `attendance-section__chef-btn--collapsed` / `--expanded` class to chef button based on row state
    - Register outside-click listener via JS interop when a row expands, unregister when all collapse
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.5, 3.1, 3.2, 4.1, 5.1, 6.1, 8.1, 8.2, 8.3, 8.4, 9.1, 9.2, 12.1, 12.2_

  - [x]* 4.2 Write property test: Accessibility attributes reflect expand/collapse state
    - **Property 8: Accessibility attributes reflect expand/collapse state**
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.4**

- [x] 5. Implement CSS transitions and responsive layout
  - [x] 5.1 Update `AttendanceSection.razor.css` with collapse/expand CSS and responsive breakpoint
    - Add `.attendance-section__btn-group` base styles: `position: relative`, `display: flex`, `align-items: center`, `gap: 8px`
    - Inside `@media (max-width: 479.98px)`:
      - Add `.attendance-section__btn-group--collapsed .attendance-section__btn` — `position: absolute`, stacked at same position, `transition: transform 250ms ease-in, opacity 150ms ease-in`
      - Add `.attendance-section__btn-group--collapsed .attendance-section__btn--active` — `position: relative`, `z-index: 2`
      - Add `.attendance-section__btn-group--collapsed .attendance-section__btn:not(.attendance-section__btn--active)` — `z-index: 1`, `opacity: 0`, `pointer-events: none`
      - Add `.attendance-section__btn-group--expanded .attendance-section__btn` — `position: relative`, `transition: transform 250ms ease-out, opacity 150ms ease-out`, `opacity: 1`, `pointer-events: auto`
      - Add `.attendance-section__chef-btn--collapsed` transition for chef button sliding
      - Add `.attendance-section__chef-btn--expanded` transition for chef button sliding left
    - Inside `@media (min-width: 480px)`:
      - Ensure `.attendance-section__btn-group` uses normal flex flow, no stacking or transitions
      - Chef button stays left via `order: -1` or natural DOM order
    - Inside `@media (max-width: 479.98px) and (hover: hover)`:
      - Add cursor pointer on collapsed active button for hover hint
    - Add `.attendance-section__name--truncated` — `max-width: calc(100% - 180px)` for name truncation when expanded
    - _Requirements: 1.2, 1.3, 2.2, 2.3, 2.4, 3.3, 3.4, 4.3, 4.4, 7.1, 7.2, 7.3, 9.1, 9.2, 9.3, 11.1, 11.3, 11.5_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Wire status change with optimistic UI and API call
  - [x] 7.1 Integrate status change flow with existing optimistic UI pattern
    - When `HandleExpandedButtonClickAsync` returns a status change (newStatus ≠ currentStatus): apply optimistic override, call API, rollback on failure
    - When collapse is triggered by re-tap (same status) or outside-click: do NOT send API request, retain current status
    - When API call fails: revert optimistic override to previous status, show error toast (existing behavior)
    - Ensure `_expandedViaHover` is cleared on any click-triggered collapse to prevent double-collapse from subsequent mouse-leave
    - _Requirements: 3.1, 3.2, 3.5, 4.2, 5.1, 5.2_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The `AttendanceRowStateManager` is extracted specifically to enable property-based testing without Blazor rendering infrastructure
- No API or backend changes are needed — this feature is purely frontend
- All CSS transitions use hardware-accelerated `transform` for smooth 60fps animations
- The 480px breakpoint ensures at least 140px horizontal space for housemate names when all buttons are visible

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "3.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "1.10", "1.11", "1.12", "1.13", "1.14", "1.15"] },
    { "id": 2, "tasks": ["4.1", "5.1"] },
    { "id": 3, "tasks": ["4.2", "7.1"] }
  ]
}
```
