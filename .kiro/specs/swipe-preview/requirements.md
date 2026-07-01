# Requirements Document

## Introduction

The swipe-preview feature enhances the DayPlanPage's existing horizontal swipe navigation by showing a live preview of the adjacent day's content sliding in from the side during the gesture. This provides spatial context (the user sees where they're going) and makes the swipe gesture more discoverable. The implementation uses a three-panel carousel approach where the current day is flanked by the previous and next day panels, with prioritized loading to ensure the active day is never delayed.

## Glossary

- **Swipe_Preview_Carousel**: The three-panel horizontal container holding the previous day, current day, and next day panels, enabling preview content to slide into view during a swipe gesture.
- **Active_Panel**: The center panel of the Swipe_Preview_Carousel that displays the currently viewed day's full DayPlanPage content.
- **Adjacent_Panel**: Either the left (previous day) or right (next day) panel in the Swipe_Preview_Carousel that shows preview content during a swipe.
- **Swipe_Threshold**: The minimum horizontal drag distance (currently 60px) required to trigger navigation to the adjacent day.
- **Arrow_Placeholder**: The existing left/right arrow indicator shown in an Adjacent_Panel when that panel's day plan data has not yet loaded.
- **Panel_Recycling**: The process of reassigning panel roles after navigation completes — the newly active panel becomes the center, and adjacent panels are updated to load the new ±1 day data.
- **DayPlanPage**: The Blazor page at route `/day/{date}` displaying attendance, dish, comments, and history for a single day.
- **ICachedApiClient**: The client-side service that provides stale-while-revalidate caching for GET requests and offline mutation queueing for writes.
- **Edge_Exclusion_Zone**: The 20px region at the left and right screen edges where touch events are ignored to allow native browser gestures.

## Requirements

### Requirement 1: Carousel Structure

**User Story:** As a user, I want to see the adjacent day's content sliding in from the side during my swipe, so that I have spatial context about where the navigation will take me.

#### Acceptance Criteria

1. WHEN the DayPlanPage renders, THE Swipe_Preview_Carousel SHALL contain exactly three panels: the previous day (left), the current day (center), and the next day (right).
2. THE Active_Panel SHALL display the full DayPlanPage content for the currently viewed date, identical to the existing DayPlanPage behavior, occupying the full viewport width with the Adjacent_Panels positioned entirely off-screen to the left and right.
3. WHEN adjacent day data has loaded, THE Adjacent_Panel SHALL display the full DayPlanPage content for that date.
4. WHILE adjacent day data has not yet loaded, THE Adjacent_Panel SHALL display the Arrow_Placeholder indicator for that direction.

### Requirement 2: Swipe Gesture Tracking with Preview

**User Story:** As a user, I want the adjacent day panel to move in sync with my finger during a horizontal swipe, so that the interaction feels direct and responsive.

#### Acceptance Criteria

1. WHEN the user drags horizontally on the DayPlanPage, THE Swipe_Preview_Carousel SHALL translate all three panels by the same number of pixels as the horizontal distance from the touch start point, updated every animation frame.
2. WHEN the user drags to the left, THE Adjacent_Panel on the right (next day) SHALL slide into view from the right edge.
3. WHEN the user drags to the right, THE Adjacent_Panel on the left (previous day) SHALL slide into view from the left edge.
4. WHILE a horizontal swipe is in progress, WHEN the drag distance exceeds the viewport width, THE Swipe_Preview_Carousel SHALL reduce additional translation at a diminishing rate so that the panels never translate beyond 1.2 times the viewport width regardless of finger position.
5. WHEN the horizontal drag distance exceeds 10px before the vertical drag distance exceeds 10px, THE Swipe_Preview_Carousel SHALL lock the gesture as horizontal and suppress vertical scrolling for the remainder of that touch sequence.
6. WHEN the vertical drag distance exceeds 10px before the horizontal drag distance exceeds 10px, THE Swipe_Preview_Carousel SHALL NOT track that touch as a horizontal swipe and SHALL allow normal vertical scrolling.

### Requirement 3: Swipe Completion

**User Story:** As a user, I want the adjacent day to slide fully into view and become the active page when I swipe past the threshold, so that navigation feels fluid.

#### Acceptance Criteria

1. WHEN the swipe distance exceeds the Swipe_Threshold, THE Swipe_Preview_Carousel SHALL animate the adjacent day panel fully into the center position over a duration of no more than 300 milliseconds.
2. WHILE the swipe completion animation is in progress, THE Swipe_Preview_Carousel SHALL NOT accept new swipe gestures.
3. WHEN the swipe animation completes, THE DayPlanPage SHALL navigate to the adjacent day's date route, identical to the current swipe navigation behavior.
4. WHEN a swipe completes successfully, THE Active_Panel SHALL become the newly navigated day's panel.

### Requirement 4: Swipe Cancellation

**User Story:** As a user, I want the view to snap back smoothly when I release a swipe below the threshold, so that accidental swipes don't disrupt my view.

#### Acceptance Criteria

1. WHEN the user releases the swipe and the drag distance is below the Swipe_Threshold, THE Swipe_Preview_Carousel SHALL animate all panels back to the starting position within 300 milliseconds.
2. WHEN the snap-back animation completes, THE Active_Panel SHALL remain in the center position showing the current day's content with panel positions identical to the pre-swipe state.
3. IF the user initiates a new swipe gesture while the snap-back animation is in progress, THEN THE Swipe_Preview_Carousel SHALL cancel the snap-back animation and begin tracking the new swipe from the panels' current position.

### Requirement 5: Prioritized Loading

**User Story:** As a user, I want the current day's content to load first without delay, so that I can interact with today's day plan immediately.

#### Acceptance Criteria

1. WHEN the DayPlanPage loads, THE DayPlanPage SHALL fetch and render the current day's data via ICachedApiClient.GetDayPlanAsync before initiating any adjacent day data fetches.
2. WHEN the current day's data has been assigned to the Active_Panel and a render cycle has completed, THE DayPlanPage SHALL initiate pre-fetching of the previous day and next day data asynchronously without awaiting their results before yielding control back to the UI thread.
3. WHILE adjacent day data is being pre-fetched, THE Active_Panel SHALL remain fully interactive, with no additional synchronous work or rendering blocked on the adjacent fetch results.
4. IF the user navigates to a different date while adjacent day pre-fetches are still in progress, THEN THE DayPlanPage SHALL discard the results of the outdated pre-fetches and restart the prioritized loading sequence for the newly viewed date.

### Requirement 6: Adjacent Day Pre-fetching

**User Story:** As a user, I want the previous and next day data pre-fetched after the current day loads, so that swiping reveals content instantly without a loading state.

#### Acceptance Criteria

1. WHEN the current day has loaded and rendered, THE DayPlanPage SHALL pre-fetch the day plan data for the date one day before the current date using ICachedApiClient.GetDayPlanAsync.
2. WHEN the current day has loaded and rendered, THE DayPlanPage SHALL pre-fetch the day plan data for the date one day after the current date using ICachedApiClient.GetDayPlanAsync.
3. WHEN pre-fetched data arrives for an adjacent day, THE Adjacent_Panel for that day SHALL replace the Arrow_Placeholder with the full day plan content.
4. IF pre-fetching of an adjacent day fails (network error or HTTP error), THEN THE Adjacent_Panel SHALL continue displaying the Arrow_Placeholder, and navigation via swipe SHALL still function by navigating to the adjacent day's route.
5. WHILE adjacent day data is being pre-fetched, THE DayPlanPage SHALL NOT show the loading indicator (LoadingIndicatorState) for the pre-fetch operations.

### Requirement 7: Fallback to Arrow Indicators

**User Story:** As a user, I want swipe navigation to always feel responsive even if adjacent data hasn't loaded yet, so that I'm never stuck waiting.

#### Acceptance Criteria

1. WHILE an Adjacent_Panel's day plan data has not loaded, THE Adjacent_Panel SHALL display the Arrow_Placeholder indicator pointing in the direction of that panel (left arrow for the previous day panel, right arrow for the next day panel).
2. WHEN the user completes a swipe toward an Adjacent_Panel that is showing the Arrow_Placeholder, THE DayPlanPage SHALL navigate to the adjacent day's route (`/day/{date}`) using NavigationManager route navigation.
3. WHEN pre-fetched data becomes available for an Adjacent_Panel currently showing the Arrow_Placeholder, THE Adjacent_Panel SHALL immediately replace the Arrow_Placeholder with the rendered day plan content without a transition animation.
4. IF pre-fetched data becomes available for an Adjacent_Panel while a swipe gesture is in progress, THEN THE Adjacent_Panel SHALL defer replacing the Arrow_Placeholder until the swipe gesture completes or is cancelled.

### Requirement 8: Panel Recycling After Navigation

**User Story:** As a user, I want smooth continuous swiping across multiple days without performance degradation, so that browsing through days feels seamless.

#### Acceptance Criteria

1. WHEN navigation to an adjacent day completes, THE Swipe_Preview_Carousel SHALL reassign panel roles so the newly navigated day becomes the Active_Panel.
2. WHEN panel roles are reassigned, THE panel that was the Active_Panel before navigation SHALL become the new Adjacent_Panel on the opposite side of the navigation direction.
3. WHEN panel roles are reassigned, THE panel that is no longer adjacent (two days away from the new current date) SHALL release its rendered content and revert to the Arrow_Placeholder state.
4. WHEN panel roles are reassigned, THE DayPlanPage SHALL initiate pre-fetching of the new adjacent day data that is not already loaded.
5. IF the user swipes rapidly to navigate multiple days in quick succession, THEN THE Swipe_Preview_Carousel SHALL complete each navigation before accepting the next swipe gesture, ensuring panel roles are consistent.

### Requirement 9: Touch Exclusion Zones

**User Story:** As a user, I want edge gestures and input field interactions to continue working normally, so that native browser navigation and text editing are not disrupted.

#### Acceptance Criteria

1. WHEN a touch starts within 20 CSS pixels of the left or right viewport edge (clientX ≤ 20 or clientX ≥ viewport width − 20), THE Swipe_Preview_Carousel SHALL NOT initiate swipe tracking for that touch.
2. WHEN a touch starts on or inside an input, textarea, select, or contenteditable element (including any descendant of such an element), THE Swipe_Preview_Carousel SHALL NOT initiate swipe tracking for that touch.
3. WHEN a touch starts inside an element with role "dialog" or inside a modal overlay element, THE Swipe_Preview_Carousel SHALL NOT initiate swipe tracking for that touch.
4. THE Swipe_Preview_Carousel SHALL evaluate exclusion zones only at touch start time; once a touch is accepted or rejected, subsequent movement of that touch SHALL NOT change the determination.

### Requirement 10: Performance — No Impact on Active Panel

**User Story:** As a user, I want the current day to remain responsive at all times, so that pre-rendering adjacent panels does not slow down my interaction with today's content.

#### Acceptance Criteria

1. THE DayPlanPage SHALL render the Active_Panel's content to the DOM before initiating any rendering of Adjacent_Panel content.
2. WHILE pre-rendering Adjacent_Panels, THE Active_Panel SHALL maintain a frame rate of at least 60 frames per second and respond to user input (tap, scroll, text entry) within 100 milliseconds.
3. IF pre-rendering an Adjacent_Panel causes any single frame on the Active_Panel to exceed 16 milliseconds or any user input response to exceed 100 milliseconds, THEN THE DayPlanPage SHALL defer the remaining Adjacent_Panel rendering until no user input has occurred on the Active_Panel for at least 200 milliseconds.

## Caching Notes (Out of Scope — Flagged for Awareness)

The following caching considerations arise from this feature but are handled by the separate offline-cache spec:

- Pre-fetched ±1 day data uses the existing `ICachedApiClient.GetDayPlanAsync` which writes to IndexedDB via the stale-while-revalidate pattern. No additional caching mechanism is needed.
- The existing `dayPlanCache` IndexedDB store (max 30 entries per household with LRU eviction) is sufficient for holding the ±1 pre-fetched entries alongside the current day.
- If swiping through many consecutive days proves to thrash the cache, a future optimization could increase the cache limit or add prioritization — but this is not expected to be needed with the current 30-entry limit.
