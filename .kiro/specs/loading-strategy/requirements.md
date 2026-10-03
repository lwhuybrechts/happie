# Requirements Document

## Introduction

This specification defines a consistent loading indicator strategy for the Happie PWA. The app currently uses multiple patterns (header spinner, delayed text, plain text, overlay, empty states) inconsistently across pages. This feature formalizes when each pattern applies, fixes existing inconsistencies, and ensures new features follow the same rules.

## Glossary

- **Loading_Strategy**: The set of rules governing which visual loading indicator to display in each data-fetching scenario
- **Header_Spinner**: The 16px spinning circle rendered by the `LoadingIndicator` component in the mobile header and sidebar, driven by `LoadingIndicatorState` with a 500ms minimum visibility
- **Inline_Loading_Dots**: Three small bouncing dots rendered inside a content area as the primary loading indicator for cold fetches, using the `.loading-delayed` CSS class (200ms delayed fade-in) so that cache hits resolve before the dots become visible
- **Stale_Overlay**: A pattern where existing content is shown at reduced opacity (`opacity: 0.4`, `pointer-events: none`) with a centered `LoadingIndicator` spinner on top, indicating data is being refreshed in place
- **Panel_Loading_Indicator**: The three-bouncing-dots animation shown inside a sub-panel (e.g., SummaryPanel, IngredientsPanel, InstructionsPanel) while the panel independently fetches its data; uses the same dot style as Inline_Loading_Dots but centered within the panel body
- **Cold_Fetch**: A data fetch where no cached result is available, requiring the user to wait for a network response before content can be displayed
- **Background_Refresh**: A data fetch that runs behind already-displayed cached content, using stale-while-revalidate caching
- **Empty_State**: A message shown when data has loaded successfully but contains no items (e.g., "No ingredients added yet")
- **Steering_Document**: A markdown file in `.kiro/steering/` that provides conventions and rules for the development team and AI assistants

## Requirements

### Requirement 1: Loading Strategy Categories

**User Story:** As a developer, I want clear categories for when each loading indicator type applies, so that I can consistently implement loading states in new and existing features.

#### Acceptance Criteria

1. THE Loading_Strategy SHALL define the following mutually exclusive categories: Header_Spinner for Background_Refresh fetches, Inline_Loading_Dots for Cold_Fetch of primary page content, Stale_Overlay for refreshing visible stale data in place, and Panel_Loading_Indicator for independently-loading sub-panels within an already-rendered parent; WHEN a sub-panel performs a Cold_Fetch, Panel_Loading_Indicator SHALL take precedence over Inline_Loading_Dots
2. WHEN a page uses stale-while-revalidate caching and cached data is already displayed, THE Loading_Strategy SHALL prescribe the Header_Spinner to indicate the Background_Refresh
3. WHEN a page or section performs a Cold_Fetch with no cached data available, THE Loading_Strategy SHALL prescribe Inline_Loading_Dots with the `.loading-delayed` CSS class
4. WHEN a user switches a filter or time range and stale data from the previous selection is still rendered, THE Loading_Strategy SHALL prescribe the Stale_Overlay pattern until the refresh completes or fails, at which point the overlay SHALL be removed and replaced with the updated content or an error indication
5. WHEN a parent page has loaded but child sub-panels fetch their own data independently, THE Loading_Strategy SHALL prescribe Panel_Loading_Indicator inside each sub-panel until its fetch completes successfully, completes with empty data, or fails
6. IF a fetch fails under any loading category, THEN THE Loading_Strategy SHALL prescribe removing the active loading indicator and displaying an inline error message indicating the failure within the affected content area

### Requirement 2: DishDetailsPage Sub-Panel Loading State

**User Story:** As a user viewing a dish's details, I want to see a loading indicator in each recipe panel while data loads, so that I do not confuse loading states with empty data.

#### Acceptance Criteria

1. WHILE SummaryPanel is fetching its data, THE SummaryPanel SHALL display a Panel_Loading_Indicator instead of the Empty_State message
2. WHILE IngredientsPanel is fetching its data, THE IngredientsPanel SHALL display a Panel_Loading_Indicator instead of the Empty_State message
3. WHILE InstructionsPanel is fetching its data, THE InstructionsPanel SHALL display a Panel_Loading_Indicator instead of the Empty_State message
4. WHEN a sub-panel fetch completes with an empty result, THE sub-panel SHALL display the appropriate Empty_State message
5. WHEN a sub-panel fetch completes with data, THE sub-panel SHALL display the loaded content
6. THE Panel_Loading_Indicator SHALL use the `.loading-delayed` CSS class so that cache-fast responses under 200ms do not flash the indicator
7. IF a sub-panel fetch fails due to a network error or non-success HTTP status, THEN THE sub-panel SHALL display the Empty_State message rather than remaining in the loading state
8. WHILE a sub-panel is in the loading state, THE sub-panel SHALL hide the edit button until the fetch completes

### Requirement 3: HousematesPage Loading Consistency

**User Story:** As a user on the housemates page, I want the loading experience to match the rest of the app, so that loading states feel consistent.

#### Acceptance Criteria

1. WHILE HousematesPage is performing a Cold_Fetch, THE HousematesPage SHALL display Inline_Loading_Dots with the `.loading-delayed` CSS class
2. THE HousematesPage SHALL render the same three-bouncing-dots component used by DayPlanPage and SavedDishesPage for their cold-fetch loading state

### Requirement 4: Steering Document Creation

**User Story:** As a developer, I want a steering document that describes the loading strategy, so that future features automatically follow the same conventions.

#### Acceptance Criteria

1. THE Loading_Strategy SHALL be documented in a steering file at `.kiro/steering/loading-strategy.md` with a YAML frontmatter block specifying `inclusion: fileMatch` and a `fileMatchPattern` that matches Blazor component files (consistent with how `ui-conventions.md` is auto-loaded)
2. THE steering document SHALL contain a section for each of the four loading categories defined in the Loading_Strategy (Header_Spinner, Inline_Loading_Dots, Stale_Overlay, Panel_Loading_Indicator), and each section SHALL include: the scenario when that category applies, and a Blazor code example demonstrating correct usage
3. THE steering document SHALL explain the role of `LoadingIndicator`, `LoadingIndicatorState`, the `LoadingDots` component, and the `.loading-delayed` CSS class, including which loading category each is used in
4. THE steering document SHALL state that Empty_State messages are reserved exclusively for scenarios where data has loaded successfully and is genuinely empty, and SHALL include at least one correct-usage and one incorrect-usage example to clarify the boundary between loading states and empty states
5. THE steering document SHALL be added as a row in the steering file index table in `project-context.md` with columns: File (`loading-strategy.md`), Topic (loading indicators and loading state conventions), and Loaded when (specifying the file patterns that trigger auto-inclusion)

### Requirement 5: Loading Dots Visual Design

**User Story:** As a user, I want loading indicators to be visually subtle and distinct from the header spinner, so that I can distinguish between background refreshes and content loading.

#### Acceptance Criteria

1. THE Inline_Loading_Dots and Panel_Loading_Indicator SHALL render as three small circles (dots) that bounce up and down in sequence, creating a wave-like animation
2. THE dots SHALL be visually distinct from the Header_Spinner (which is a spinning circle) so users can differentiate background refreshes from content loading
3. THE dots SHALL be rendered by a reusable `LoadingDots` component that can be placed inline or centered within a container
4. EACH dot SHALL be approximately 8px in diameter with a subtle color matching the app's text color (#1a1a2e) at reduced opacity
5. THE bounce animation SHALL have a total cycle duration between 1.0s and 1.4s with staggered timing across the three dots
6. WHILE the user has `prefers-reduced-motion` enabled, THE dots SHALL use an opacity fade animation (no vertical movement) as a fallback
7. THE `LoadingDots` component SHALL include an accessible `role="status"` attribute and an `aria-label` using the `LoadingIndicator_AriaLabel` localization key
8. WHEN used for Inline_Loading_Dots or Panel_Loading_Indicator, THE dots SHALL be wrapped in a container with the `.loading-delayed` CSS class so they remain invisible for the first 200ms
