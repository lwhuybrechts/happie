# Requirements Document

## Introduction

This feature adds inline ghost text autocomplete to the custom dish text field in the DayPlan page's DishPanel component. As the user types a dish description, the system suggests matching saved dishes by displaying the remaining text as grey ghost text inline with the input. The user can accept the suggestion with a single action (Tab/Right arrow on desktop, tap on the ghost text on mobile) or simply continue typing to dismiss it. The input field supports multiple dishes separated by " & " and the autocomplete applies only to the last segment. The approach is non-blocking (no dropdowns) and works seamlessly across desktop and mobile.

## Glossary

- **Autocomplete_Engine**: The client-side logic that matches the user's typed input against saved dish names and determines the best suggestion to display.
- **Ghost_Text**: The semi-transparent, greyed-out text rendered inline after the user's typed input, representing the untyped remainder of a matched saved dish name.
- **Accept_Action**: The user interaction that confirms the ghost text suggestion and completes the current dish segment with the full matched dish name. On desktop this is Tab or Right arrow; on mobile this is tapping the ghost text.
- **DishPanel**: The existing Blazor component that displays the day's dinner time, free-text dish description, and linked saved dishes with add/remove controls.
- **Saved_Dish_List**: The collection of reusable saved dishes managed on the SavedDishesPage and retrieved via the cached API client.
- **Dish_Delimiter**: The literal string " & " (space-ampersand-space) that separates multiple dish names within the custom dish input field.
- **Active_Segment**: The portion of the input text after the last Dish_Delimiter, or the entire input text if no delimiter is present. This is the text evaluated for autocomplete matching.

## Requirements

### Requirement 1: Suggestion Matching

**User Story:** As a housemate, I want the dish input to suggest saved dishes as I type, so that I can quickly reuse previously saved dish names without typing the full name.

#### Acceptance Criteria

1. WHEN the Active_Segment contains at least 1 character, THE Autocomplete_Engine SHALL find the first non-deleted saved dish whose description starts with the Active_Segment using case-insensitive, ordinal prefix matching.
2. WHEN multiple non-deleted saved dishes match the Active_Segment prefix, THE Autocomplete_Engine SHALL select the match whose description comes first in case-insensitive ordinal sort order.
3. WHEN no non-deleted saved dish description starts with the Active_Segment, THE Autocomplete_Engine SHALL display no Ghost_Text suggestion.
4. WHEN the Active_Segment exactly matches a non-deleted saved dish description in full (case-insensitive), THE Autocomplete_Engine SHALL display no Ghost_Text suggestion.
5. WHEN the Saved_Dish_List is empty, still loading, or failed to load, THE Autocomplete_Engine SHALL display no Ghost_Text suggestion and allow normal text input without errors.
6. WHEN the Active_Segment is empty, THE Autocomplete_Engine SHALL display no Ghost_Text suggestion.

### Requirement 2: Ghost Text Display

**User Story:** As a housemate, I want to see the suggested completion as faded text after my cursor, so that I can preview the suggestion without it interfering with my typing.

#### Acceptance Criteria

1. WHEN a matching suggestion exists, THE DishPanel SHALL render the Ghost_Text inline immediately after the user's typed text within the same input area.
2. THE Ghost_Text SHALL be visually distinct from the typed text by rendering in the same font and size but at an opacity between 0.4 and 0.6.
3. THE Ghost_Text SHALL be non-interactive: it SHALL NOT be selectable, editable, or included in the input field's form value.
4. WHILE the user continues typing characters that still match the suggestion prefix, THE DishPanel SHALL update the Ghost_Text to show only the remaining untyped portion of the suggestion.
5. WHEN the user types a character that causes the Active_Segment to no longer match any saved dish prefix, THE DishPanel SHALL remove the Ghost_Text within the same rendering frame.
6. WHEN the user clears the input field entirely, THE DishPanel SHALL remove any Ghost_Text.
7. IF the text cursor is not positioned at the end of the input text, THEN THE DishPanel SHALL hide the Ghost_Text until the cursor returns to the end position.
8. WHEN the input field loses focus, THE DishPanel SHALL hide the Ghost_Text.
9. THE Ghost_Text SHALL display only the untyped remainder of the matched dish name for the Active_Segment, not the full matched dish name or any preceding segments.

### Requirement 3: Accept Suggestion

**User Story:** As a housemate, I want to accept the suggested dish name with a single action, so that I can complete my input quickly on both desktop and mobile.

#### Acceptance Criteria

1. WHEN Ghost_Text is visible and the user presses the Tab key, THE DishPanel SHALL replace the Active_Segment with the full matched saved dish name, remove the Ghost_Text, and place the cursor at the end of the accepted text while retaining focus in the input field.
2. WHEN Ghost_Text is visible and the cursor is at the end of the typed text and the user presses the Right arrow key, THE DishPanel SHALL replace the Active_Segment with the full matched saved dish name, remove the Ghost_Text, and place the cursor at the end of the accepted text.
3. WHEN Ghost_Text is visible and the cursor is not at the end of the typed text and the user presses the Right arrow key, THE DishPanel SHALL perform the default cursor movement behavior without accepting the suggestion.
4. WHEN no Ghost_Text is visible and the user presses Tab, THE DishPanel SHALL perform the default Tab behavior (move focus to the next focusable element).
5. WHEN no Ghost_Text is visible and the user presses Right arrow, THE DishPanel SHALL perform the default cursor movement behavior.
6. WHEN Ghost_Text is visible and the user taps on the Ghost_Text, THE DishPanel SHALL replace the Active_Segment with the full matched saved dish name, remove the Ghost_Text, and retain focus in the input field.

### Requirement 4: Multi-Dish Delimiter Handling

**User Story:** As a housemate, I want to type multiple dishes separated by " & " and still get autocomplete suggestions for the dish I am currently typing, so that I can quickly compose a multi-dish description.

#### Acceptance Criteria

1. WHEN the input text contains one or more Dish_Delimiter occurrences, THE Autocomplete_Engine SHALL extract the Active_Segment as the text after the last Dish_Delimiter and use only the Active_Segment for prefix matching.
2. WHEN the input text contains no Dish_Delimiter, THE Autocomplete_Engine SHALL treat the entire input text as the Active_Segment.
3. WHEN the user accepts a suggestion via Accept_Action, THE DishPanel SHALL replace only the Active_Segment with the full matched saved dish name, preserving all preceding text and delimiters unchanged.
4. WHEN the Active_Segment is empty immediately after a Dish_Delimiter, THE Autocomplete_Engine SHALL display no Ghost_Text suggestion.

### Requirement 5: Non-Blocking Interaction

**User Story:** As a housemate, I want the autocomplete to never block my typing or obscure the UI, so that I can freely type a custom dish name at any time.

#### Acceptance Criteria

1. THE DishPanel SHALL display suggestions exclusively as inline Ghost_Text without rendering dropdown menus, popup lists, or overlay elements.
2. WHEN the user continues typing characters that diverge from the suggestion, THE DishPanel SHALL dismiss the Ghost_Text within 16 milliseconds of the keystroke event and preserve all typed characters in their original order without modification or loss.
3. WHEN the user presses Backspace, THE Autocomplete_Engine SHALL re-evaluate the Active_Segment against the Saved_Dish_List and display a new Ghost_Text suggestion if a match exists, or remove any existing Ghost_Text if no match exists.
4. THE Autocomplete_Engine SHALL complete suggestion matching within 16 milliseconds to avoid perceptible input lag.
5. WHEN the custom dish input field loses focus, THE DishPanel SHALL remove any visible Ghost_Text.

### Requirement 6: Saved Mode Exclusion

**User Story:** As a housemate, I want autocomplete to only appear in custom text mode, so that it does not interfere with the saved dish selection workflow.

#### Acceptance Criteria

1. WHILE the DishPanel is in saved dish mode, THE Autocomplete_Engine SHALL not display any Ghost_Text suggestions.
2. WHEN the user switches from saved mode to custom mode, THE Autocomplete_Engine SHALL evaluate the current Active_Segment for matching suggestions.
