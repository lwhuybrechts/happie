# Implementation Plan: Day Plan Redesign

## Overview

This plan implements the visual and structural redesign of the Happie PWA's main layout and Day Plan page. The work is organized into backend DTO extensions, frontend utility services, layout components (sidebar, mobile header, bottom nav), and Day Plan page sub-components (date navigation, dish panel, attendance, comments, history, nudge modal). Each task builds incrementally on the previous, ending with full integration.

## Tasks

- [x] 1. Extend backend DTOs and entities
  - [x] 1.1 Extend DishDto, DishRecordEntity, DishRecord domain type, and DishRecordMapper to include `LastChangedByHousemateId` (Guid?) and `LastChangedAt` (DateTimeOffset?)
    - Add `LastChangedByHousemateId` and `LastChangedAt` properties to `DishDto` in `Happie.Shared/Contracts/DishDto.cs`
    - Add `LastChangedByHousemateId` and `LastChangedAt` to `DishRecordEntity` in `Happie.Api/Infrastructure/Entities/DishRecordEntity.cs`
    - Add `LastChangedByHousemateId` and `LastChangedAt` to `DishRecord` domain type in `Happie.Api/Domain/DishRecord.cs`
    - Update `DishRecordMapper` to map the new fields in both directions
    - Update `DayHandler` to populate the new fields when building `DishDto` in the day plan response
    - Update `DayHandler` to set `LastChangedByHousemateId` and `LastChangedAt` when saving a dish
    - _Requirements: 11.3, 11.5_

  - [x] 1.2 Extend CommentDto and CommentEntity to include `LastEditedAt` (DateTimeOffset?)
    - Add `LastEditedAt` to `CommentDto` in `Happie.Shared/Contracts/CommentDto.cs`
    - Add `LastEditedAt` to `CommentEntity` in `Happie.Api/Infrastructure/Entities/CommentEntity.cs`
    - Add `LastEditedAt` to `Comment` domain type in `Happie.Api/Domain/Comment.cs`
    - Update `CommentMapper` to map the new field
    - Update `DayHandler` to populate `LastEditedAt` when building `CommentDto`
    - Update `DayHandler` to set `LastEditedAt` when saving a comment
    - _Requirements: 14.2_

  - [x] 1.3 Extend HistoryEntryDto to include `ChangedByHousemateId` (Guid)
    - Add `ChangedByHousemateId` to `HistoryEntryDto` in `Happie.Shared/Contracts/HistoryEntryDto.cs`
    - Update `DayHandler` to populate `ChangedByHousemateId` when building `HistoryEntryDto`
    - _Requirements: 15.3_

- [x] 2. Implement frontend utility services
  - [x] 2.1 Create `DateLabelService` static class in `Happie.Web/Services/DateLabelService.cs`
    - Implement `GetLabel(DateOnly viewedDate, DateOnly today, CultureInfo culture)` returning `DateLabel` record
    - Logic: offset 0 → "Today", -1 → "Yesterday", +1 → "Tomorrow", ±2–6 → day name, ≥7 → null title with bold date
    - Date format: `d MMM yyyy` using provided CultureInfo for locale-aware month abbreviation
    - _Requirements: 10.4, 10.5, 10.6, 10.7, 10.8, 10.9_

  - [x]* 2.2 Write property test for DateLabelService contextual title correctness
    - **Property 1: Date label contextual title correctness**
    - **Validates: Requirements 10.4, 10.5, 10.6, 10.7, 10.8**

  - [x]* 2.3 Write property test for DateLabelService locale-aware month abbreviation
    - **Property 2: Date label formatted date uses locale-aware month abbreviation**
    - **Validates: Requirements 10.9**

  - [x] 2.4 Create `TimeFormatter` static class in `Happie.Web/Services/TimeFormatter.cs`
    - Implement `FormatDishTime(DateTimeOffset editedAt, DateTimeOffset now)` with rules: <60s → "just now", <60min → "{N} min ago", <3h → "{N} hours ago", ≥3h same day → HH:mm, previous day → "d MMM HH:mm"
    - Implement `FormatHistoryTime(DateTimeOffset changedAt, DateTimeOffset now)` with rules: same day → HH:mm, different day same year → "d MMM HH:mm", previous year → "d MMM yyyy HH:mm"
    - _Requirements: 11.5, 15.4, 15.5, 15.6_

  - [x]* 2.5 Write property test for TimeFormatter.FormatDishTime
    - **Property 3: Dish relative time formatting**
    - **Validates: Requirements 11.5**

  - [x]* 2.6 Write property test for TimeFormatter.FormatHistoryTime
    - **Property 4: History timestamp formatting**
    - **Validates: Requirements 15.4, 15.5, 15.6**

  - [x] 2.7 Create `ActiveHousemateService` in `Happie.Web/Services/ActiveHousemateService.cs`
    - Implement `Id`, `Name`, `Color` properties
    - Implement `InitializeAsync()` that reads active housemate data from localStorage
    - Persist active housemate name and color in localStorage at selection time (update login flow)
    - Register as scoped service in `Program.cs`
    - _Requirements: 3.1, 8.3_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement layout components
  - [x] 4.1 Create `HousemateAvatar` component in `Happie.Web/Components/HousemateAvatar.razor`
    - Render a 36×36px colored rounded square with the first letter of the name in white
    - Parameters: `Name`, `Color`, `OnClick` (EventCallback), `ShowHoverEffect` (bool)
    - When `ShowHoverEffect` is true and hovered, show green outline (#4CAF50) and pointer cursor
    - Add scoped CSS file `HousemateAvatar.razor.css`
    - _Requirements: 3.1, 3.2, 8.3_

  - [x] 4.2 Create `Sidebar` component in `Happie.Web/Layout/Sidebar.razor`
    - Display Happie logo (green rounded square with white "H") + "Happie" text as topmost element
    - Display navigation items: "On the menu", "Calendar", "Housemates" — all navigating to `/day/{today}` for now
    - Display "Log Out" link that clears localStorage and navigates to `/`
    - Display tagline in muted text with rounded corners above locale switcher
    - Display `LocaleSwitcher` in bottom-left area
    - Display `HousemateAvatar` for active housemate in bottom-left, clicking navigates to `/housemates`
    - Add scoped CSS file `Sidebar.razor.css`
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 4.1, 4.2, 6.1, 6.2, 6.3, 7.1_

  - [x] 4.3 Create `MobileHeader` component in `Happie.Web/Layout/MobileHeader.razor`
    - Display "Happie" text in top-left (no logo)
    - Display `LocaleSwitcher` to the left of the avatar
    - Display `HousemateAvatar` for active housemate in top-right, clicking navigates to `/housemates`
    - Fixed at top of viewport
    - Add scoped CSS file `MobileHeader.razor.css`
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

  - [x] 4.4 Create `MobileBottomNav` component in `Happie.Web/Layout/MobileBottomNav.razor`
    - Display floating bar fixed at bottom with icons: "On the menu", "Calendar", "Housemates"
    - All icons navigate to `/day/{today}` until dedicated pages are implemented
    - Highlight the currently active page icon
    - No logout icon
    - Add scoped CSS file `MobileBottomNav.razor.css`
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

  - [x] 4.5 Redesign `MainLayout.razor` to integrate new layout components
    - Remove the top header bar for desktop (≥641px)
    - Render `Sidebar` for desktop viewports (≥641px)
    - Render `MobileHeader` for mobile viewports (<641px)
    - Render `MobileBottomNav` for mobile viewports (<641px)
    - Hide sidebar on mobile, hide mobile components on desktop
    - Update `MainLayout.razor.css` with responsive media queries at 641px breakpoint
    - Remove or replace `NavMenu.razor` and `NavMenu.razor.css`
    - _Requirements: 5.1, 8.7, 9.1_

- [x] 5. Checkpoint - Ensure layout renders correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement Day Plan page sub-components
  - [x] 6.1 Create `DateNavigationPanel` component in `Happie.Web/Components/DateNavigationPanel.razor`
    - Display left/right arrow buttons for previous/next day navigation
    - Use `DateLabelService` to compute contextual title and formatted date
    - Render as floating panel with rounded corners
    - Parameters: `ViewedDate`, `OnPreviousDay`, `OnNextDay`
    - Add scoped CSS file `DateNavigationPanel.razor.css`
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9, 10.10_

  - [x] 6.2 Create `DishPanel` component in `Happie.Web/Components/DishPanel.razor`
    - Display food icon and "on the menu" text
    - Display "What are we eating?" with edit icon when no dish is set
    - Display dish text, last editor name, and relative time (via `TimeFormatter`) when dish exists
    - Implement inline editing: text input (max 100 chars), accept/discard buttons
    - Optimistic save with rollback on failure
    - Add scoped CSS file `DishPanel.razor.css`
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 12.1, 12.2, 12.3, 12.4, 12.5_

  - [x] 6.3 Create `AttendanceSection` component in `Happie.Web/Components/AttendanceSection.razor`
    - Display "Attendance" header with nudge button on the same row (right-aligned)
    - List all housemates with avatar + name (same layout as login page housemate selection)
    - Display three option buttons per housemate: V (green), ? (neutral), X (red)
    - Highlight the active status button with the correct color
    - Optimistic toggle with rollback on failure
    - Add scoped CSS file `AttendanceSection.razor.css`
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7, 13.8, 16.1, 16.2, 16.3_

  - [x]* 6.4 Write property test for attendance status button highlight mapping
    - **Property 5: Attendance status button highlight mapping**
    - **Validates: Requirements 13.4, 13.5, 13.6**

  - [x] 6.5 Create `CommentsSection` component in `Happie.Web/Components/CommentsSection.razor`
    - Display "Comments" header
    - Display placed comments ordered by `LastEditedAt` (most recent first)
    - Each comment shows avatar, name, and text
    - Hide non-active housemates without comments
    - Show dotted outline placeholder for active housemate when no comment exists
    - Green hover effect on placeholder, click enters edit mode
    - Edit mode: text input (max 200 chars), save/discard buttons
    - Click existing comment to edit (pre-populated)
    - Optimistic save with rollback on failure
    - Add scoped CSS file `CommentsSection.razor.css`
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7, 14.8, 14.9, 14.10, 14.11_

  - [x] 6.6 Create `HistorySection` component in `Happie.Web/Components/HistorySection.razor`
    - Display header with back-in-time icon and "History" text
    - Display entries in reverse-chronological order
    - Each entry shows avatar, name, grey clock icon, formatted timestamp (via `TimeFormatter`)
    - Display change description below name/timestamp line
    - Show "no changes" message when no history entries exist for the viewed day
    - Resolve housemate color from attendance list, fallback for soft-deleted housemates
    - Add scoped CSS file `HistorySection.razor.css`
    - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5, 15.6, 15.7, 15.8_

  - [x] 6.7 Create `NudgeModal` component in `Happie.Web/Components/NudgeModal.razor`
    - Overlay with blurred background
    - Bell icon + "Send a nudge" text, X close button
    - Recipient list: housemates with Unknown status (excluding active), all pre-selected
    - "Predefined" / "Custom" message toggle, predefined selected by default
    - Predefined: 3 selectable message options, first selected by default
    - Custom: text input (max 20 chars)
    - Green "Send Nudge" button with paper airplane icon, disabled when no recipients selected
    - Send nudge on click, close modal
    - X button closes without sending
    - Add scoped CSS file `NudgeModal.razor.css`
    - _Requirements: 17.1, 17.2, 17.3, 17.4, 17.5, 17.6, 17.7, 17.8, 17.9, 17.10_

  - [x]* 6.8 Write property test for nudge recipient filtering
    - **Property 6: Nudge recipient filtering**
    - **Validates: Requirements 17.3**

  - [x]* 6.9 Write property test for nudge send button disabled state
    - **Property 7: Nudge send button disabled state**
    - **Validates: Requirements 17.8**

- [x] 7. Checkpoint - Ensure all sub-components render correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Wire Day Plan page and content centering
  - [x] 8.1 Restructure `DayPlanPage.razor` to use new sub-components
    - Replace existing inline sections with `DateNavigationPanel`, `DishPanel`, `AttendanceSection`, `CommentsSection`, `HistorySection`
    - Wire `NudgeModal` with `Open()` method triggered from `AttendanceSection` nudge button
    - Handle navigation callbacks (prev/next day)
    - Handle mutation callbacks (dish change, attendance toggle, comment save) with API calls
    - Remove old component references (`DishEditor`, `DayHistoryLog`, `NudgeDialog`, `AttendanceToggle`, `CommentEditor`)
    - _Requirements: 10.1, 10.2, 10.3, 11.1, 12.1, 13.1, 14.1, 15.1, 16.1, 17.1_

  - [x] 8.2 Implement content centering for Day Plan page
    - Desktop (≥641px): center content within available space (viewport minus sidebar), max-width 600px, equal margins
    - Mobile (<641px): full viewport width with 16px horizontal padding
    - Add/update scoped CSS in `DayPlanPage.razor.css`
    - _Requirements: 18.1, 18.2, 18.3_

- [x] 9. Clean up removed components
  - [x] 9.1 Remove old components that have been replaced
    - Delete `Happie.Web/Components/DishEditor.razor` (replaced by `DishPanel`)
    - Delete `Happie.Web/Components/DayHistoryLog.razor` (replaced by `HistorySection`)
    - Delete `Happie.Web/Components/NudgeDialog.razor` (replaced by `NudgeModal`)
    - Delete `Happie.Web/Layout/NavMenu.razor` and `NavMenu.razor.css` (replaced by `Sidebar` and `MobileBottomNav`)
    - Remove any remaining references to deleted components
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# throughout (Blazor WebAssembly + .NET 10), so no language selection was needed
- The `LocaleSwitcher` component already exists and will be reused in the Sidebar and MobileHeader
- The `AttendanceToggle` component is NOT deleted — it may still be used elsewhere; the new `AttendanceSection` replaces its usage on the Day Plan page

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "2.1", "2.4", "2.7"] },
    { "id": 1, "tasks": ["2.2", "2.3", "2.5", "2.6", "4.1"] },
    { "id": 2, "tasks": ["4.2", "4.3", "4.4"] },
    { "id": 3, "tasks": ["4.5"] },
    { "id": 4, "tasks": ["6.1", "6.2", "6.3", "6.5", "6.6", "6.7"] },
    { "id": 5, "tasks": ["6.4", "6.8", "6.9"] },
    { "id": 6, "tasks": ["8.1", "8.2"] },
    { "id": 7, "tasks": ["9.1"] }
  ]
}
```
