# Implementation Plan: Dish Autocomplete

## Overview

This plan implements inline ghost text autocomplete for the custom dish input field in the DishPanel component. The implementation is split into: shared constant extraction, the pure-function matching engine with property tests, JS interop helpers, and the DishPanel UI integration with component tests.

## Tasks

- [x] 1. Create shared delimiter constant and autocomplete engine
  - [x] 1.1 Create `DishConstants` in `Happie.Shared/Domain/DishConstants.cs`
    - Define `public const string Delimiter = " & ";`
    - Namespace: `Happie.Shared.Domain`
    - _Requirements: 4.1, 4.2_

  - [x] 1.2 Update `SavedDishMatcher` to use `DishConstants.Delimiter`
    - Replace hardcoded `" & "` string references with `DishConstants.Delimiter`
    - Add `using Happie.Shared.Domain;`
    - _Requirements: 4.1_

  - [x] 1.3 Implement `DishAutocompleteEngine` in `Happie.Web/Services/DishAutocompleteEngine.cs`
    - Implement `ExtractActiveSegment(string inputText)` — splits on `DishConstants.Delimiter`, returns text after last delimiter or entire input if no delimiter
    - Implement `GetSuggestion(string activeSegment, IReadOnlyList<SavedDishDto>? savedDishes)` — case-insensitive ordinal prefix match, returns untyped remainder of first sorted match, null for empty/exact/no match
    - Implement `AcceptSuggestion(string inputText, string matchedDishName)` — replaces active segment with full matched dish name, preserves preceding text and delimiters
    - Skip deleted dishes (null/empty descriptions), skip empty active segments
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 4.1, 4.2, 4.3_

- [x] 2. Property tests for DishAutocompleteEngine
  - [x] 2.1 Write property test for prefix match selection
    - **Property 1: Prefix match selects the first sorted match**
    - **Validates: Requirements 1.1, 1.2**

  - [x] 2.2 Write property test for non-matching segment
    - **Property 2: Non-matching segment returns null**
    - **Validates: Requirements 1.3**

  - [x] 2.3 Write property test for exact full match
    - **Property 3: Exact full match returns null**
    - **Validates: Requirements 1.4**

  - [x] 2.4 Write property test for suggestion remainder
    - **Property 4: Suggestion is the untyped remainder**
    - **Validates: Requirements 2.4, 2.9**

  - [x] 2.5 Write property test for active segment extraction
    - **Property 5: Active segment extraction**
    - **Validates: Requirements 4.1, 4.2**

  - [x] 2.6 Write property test for accept preserving preceding text
    - **Property 6: Accept preserves preceding text**
    - **Validates: Requirements 3.1, 4.3**

- [x] 3. Unit tests for DishAutocompleteEngine
  - [x] 3.1 Write unit tests in `Happie.Web.Tests/Services/DishAutocompleteEngineTests.cs`
    - Test empty active segment returns null
    - Test null or empty dish list returns null
    - Test single-character prefix matching
    - Test case variations (upper/lower/mixed)
    - Test dishes with special characters and spaces
    - Test delimiter at end of input ("Pizza & " → empty active segment → null)
    - Test AcceptSuggestion with zero, one, and multiple delimiters
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 4.1, 4.2, 4.3, 4.4_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Add JS interop helpers and DishPanel UI integration
  - [x] 5.1 Create `wwwroot/js/dishAutocomplete.js` with cursor helpers
    - Implement `window.happie.getCursorAtEnd(inputElement)` — returns true if selectionStart and selectionEnd both equal value.length
    - Implement `window.happie.setCursorPosition(inputElement, position)` — calls setSelectionRange
    - _Requirements: 3.2, 3.3, 2.7_

  - [x] 5.2 Register the JS file in `index.html`
    - Add a `<script src="js/dishAutocomplete.js"></script>` reference
    - _Requirements: 3.2, 2.7_

  - [x] 5.3 Integrate autocomplete into `DishPanel.razor`
    - Add `_ghostText`, `_savedDishes`, `_cursorAtEnd`, `_matchedDishName` fields
    - Subscribe to `ICachedApiClient.OnSavedDishesUpdated` to refresh `_savedDishes`
    - On `oninput`: extract active segment, call `GetSuggestion`, set `_ghostText` and `_matchedDishName`
    - On `onkeydown` (Tab): if ghost text visible, prevent default, call `AcceptSuggestion`, update input value, set cursor position via JS interop
    - On `onkeydown` (Right arrow): if ghost text visible and cursor at end, prevent default, call `AcceptSuggestion`
    - On `onfocus`: re-evaluate suggestion for current active segment
    - On `onblur`: clear `_ghostText`
    - Only show ghost text when in custom (non-saved) edit mode
    - Call `getCursorAtEnd` on input events to determine ghost text visibility
    - _Requirements: 1.1, 2.1, 2.4, 2.5, 2.6, 2.7, 2.8, 3.1, 3.2, 3.3, 3.4, 3.5, 5.1, 5.2, 5.3, 6.1, 6.2_

  - [x] 5.4 Add ghost text overlay markup and CSS in `DishPanel.razor` and `DishPanel.razor.css`
    - Render a positioned `<span>` overlay for ghost text (same font/size, opacity 0.4–0.6)
    - Make ghost text non-interactive (`pointer-events: none`, `user-select: none`)
    - Add tap handler on a transparent overlay for mobile accept action
    - Container uses `overflow: hidden` for ghost text clipping on long inputs
    - _Requirements: 2.1, 2.2, 2.3, 2.9, 3.6, 5.1_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Component tests for DishPanel autocomplete
  - [x] 7.1 Write bUnit component tests in `Happie.Web.Tests/Components/DishPanelAutocompleteTests.cs`
    - Test ghost text span rendered when suggestion exists
    - Test ghost text span hidden on blur
    - Test ghost text span hidden when cursor not at end (mocked JS interop)
    - Test Tab key accepts suggestion and updates input value
    - Test Right arrow at end accepts suggestion
    - Test Right arrow not at end does not accept
    - Test tap on ghost text accepts suggestion
    - Test no ghost text in saved mode
    - Test ghost text appears after switching from saved to custom mode
    - _Requirements: 2.1, 2.7, 2.8, 3.1, 3.2, 3.3, 3.6, 5.1, 6.1, 6.2_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# throughout — no language selection was needed
- `DishAutocompleteEngine` is a pure static class with no Blazor dependencies, making it trivially testable
- The JS interop helpers are minimal (cursor position detection only)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6", "3.1"] },
    { "id": 3, "tasks": ["5.1", "5.2"] },
    { "id": 4, "tasks": ["5.3"] },
    { "id": 5, "tasks": ["5.4"] },
    { "id": 6, "tasks": ["7.1"] }
  ]
}
```
