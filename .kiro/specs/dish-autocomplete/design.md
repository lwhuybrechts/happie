# Design Document: Dish Autocomplete

## Overview

This feature adds inline ghost text autocomplete to the custom dish text field in the DishPanel component. As the user types, the system matches the active segment (text after the last " & " delimiter) against the household's saved dish list and displays the untyped remainder as semi-transparent ghost text. The user can accept the suggestion with Tab, Right arrow (desktop), or tap (mobile), or simply keep typing to dismiss it.

The feature is entirely client-side: matching runs against the saved dishes already loaded in the `ICachedApiClient` cache. No new API endpoints are required.

### Design Decisions

1. **Pure function matching engine** — The autocomplete logic is extracted into a standalone static class `DishAutocompleteEngine` with no dependencies on Blazor or UI state. This makes it trivially testable with property-based tests.
2. **Overlay span for ghost text** — Rather than modifying the `<input>` value (which would fight with Blazor's binding), ghost text is rendered as a positioned `<span>` that overlays the input. This avoids form-value contamination and cursor-position issues.
3. **No JS interop for matching** — All prefix matching is done in C# on the UI thread. Given that saved dish lists are typically <100 items, linear scan with `string.StartsWith(StringComparison.OrdinalIgnoreCase)` is well within the 16ms budget.
5. **Shared delimiter constant** — The `" & "` dish delimiter is extracted into a constant in `Happie.Shared/Domain/DishConstants.cs` so both the server-side `SavedDishMatcher` and the client-side `DishAutocompleteEngine` reference the same string. This avoids duplicating the magic string across projects.
4. **Cursor position via JS interop** — Detecting whether the cursor is at the end of the input (required for Right arrow acceptance and ghost text visibility) uses a small JS helper that reads `selectionStart`/`selectionEnd`.
5. **Ghost text clipping on overflow** — When the input text is long enough to cause horizontal scrolling, the ghost text overlay is clipped to the input's bounding box via `overflow: hidden` on the container. The browser will not auto-scroll to reveal ghost text since it is not part of the input's value. This is acceptable because: (a) the ghost text is a non-essential hint, (b) accept actions (Tab/Right arrow) still work even when ghost text is not visible, and (c) after accepting, the input value updates and the browser scrolls normally.

## Architecture

```mermaid
graph TD
    subgraph DishPanel Component
        Input["<input> element"]
        Ghost["Ghost text <span>"]
        EventHandlers["Event handlers (oninput, onkeydown, onfocus, onblur, onclick)"]
    end

    subgraph DishAutocompleteEngine (static)
        GetSuggestion["GetSuggestion(activeSegment, savedDishes)"]
        ExtractActiveSegment["ExtractActiveSegment(inputText)"]
    end

    subgraph ICachedApiClient
        GetSavedDishes["GetSavedDishesAsync()"]
        OnSavedDishesUpdated["OnSavedDishesUpdated event"]
    end

    Input -->|oninput| EventHandlers
    EventHandlers -->|extract active segment| ExtractActiveSegment
    ExtractActiveSegment -->|prefix match| GetSuggestion
    GetSuggestion -->|ghost text value| Ghost
    ICachedApiClient -->|saved dish list| GetSuggestion
    OnSavedDishesUpdated -->|refresh list| EventHandlers
```

### Data Flow

1. User types in the dish input field → `oninput` fires
2. `ExtractActiveSegment` splits the input on `" & "` and returns the text after the last delimiter
3. `GetSuggestion` performs case-insensitive ordinal prefix matching against non-deleted saved dishes, selects the first match in case-insensitive ordinal sort order, and returns the untyped remainder (or `null`)
4. The ghost text span is rendered (or hidden) based on the result
5. On accept action (Tab / Right arrow / tap), the active segment is replaced with the full matched dish name

## Components and Interfaces

### `DishConstants` (shared)

Location: `Happie.Shared/Domain/DishConstants.cs`

```csharp
namespace Happie.Shared.Domain;

/// <summary>Shared constants for dish-related logic used by both client and server.</summary>
public static class DishConstants
{
    /// <summary>The delimiter separating multiple dishes in a dish description.</summary>
    public const string Delimiter = " & ";
}
```

### `DishAutocompleteEngine` (static class)

Location: `Happie.Web/Services/DishAutocompleteEngine.cs`

```csharp
namespace Happie.Web.Services;

/// <summary>Client-side autocomplete matching engine for the dish input field.</summary>
public static class DishAutocompleteEngine
{
    /// <summary>
    /// Extracts the active segment from the full input text.
    /// The active segment is the text after the last delimiter, or the entire text if no delimiter exists.
    /// Uses <see cref="DishConstants.Delimiter"/> from Happie.Shared.
    /// </summary>
    public static string ExtractActiveSegment(string inputText);

    /// <summary>
    /// Finds the best autocomplete suggestion for the given active segment.
    /// Returns the untyped remainder of the matched dish name, or null if no match.
    /// </summary>
    public static string? GetSuggestion(string activeSegment, IReadOnlyList<SavedDishDto>? savedDishes);

    /// <summary>
    /// Builds the accepted text by replacing the active segment with the full matched dish name.
    /// Preserves all preceding text and delimiters.
    /// Uses <see cref="DishConstants.Delimiter"/> from Happie.Shared.
    /// </summary>
    public static string AcceptSuggestion(string inputText, string matchedDishName);
}
```

### DishPanel Changes

The existing `DishPanel.razor` component gains:
- A `_ghostText` field holding the current suggestion remainder (or `null`)
- A `_savedDishes` field holding the cached saved dish list
- An overlay `<span>` rendered conditionally when `_ghostText` is not null and the field is in custom (non-saved) edit mode
- Event handlers for Tab, Right arrow, and ghost text tap
- A JS interop call to check cursor position (`happie.getCursorAtEnd`)
- Subscription to `ICachedApiClient.OnSavedDishesUpdated` for list refresh

### JS Interop Additions

A small helper added to `wwwroot/js/dishAutocomplete.js`:

```javascript
window.happie.getCursorAtEnd = (inputElement) => {
    return inputElement.selectionStart === inputElement.value.length
        && inputElement.selectionEnd === inputElement.value.length;
};

window.happie.setCursorPosition = (inputElement, position) => {
    inputElement.setSelectionRange(position, position);
};
```

## Data Models

No new data models are introduced. The feature uses existing types:

| Type | Location | Usage |
|---|---|---|
| `SavedDishDto` | `Happie.Shared/Contracts/` | Represents a saved dish with `Id` and `Description` |
| `SavedDishesFetchResult` | `Happie.Web/Services/Caching/` | Return type of `GetSavedDishesAsync()` containing the dish list |

### State in DishPanel

| Field | Type | Description |
|---|---|---|
| `_ghostText` | `string?` | The untyped remainder to display as ghost text, or null |
| `_savedDishes` | `IReadOnlyList<SavedDishDto>?` | Cached saved dishes list for matching |
| `_cursorAtEnd` | `bool` | Whether the cursor is at the end of the input |
| `_matchedDishName` | `string?` | The full matched dish name (needed for accept action) |


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Prefix match selects the first sorted match

*For any* non-empty active segment and any non-empty list of non-deleted saved dishes, if `GetSuggestion` returns a non-null result, the matched dish description SHALL start with the active segment (case-insensitive ordinal) AND no other matching dish in the list SHALL sort before it using case-insensitive ordinal comparison.

**Validates: Requirements 1.1, 1.2**

### Property 2: Non-matching segment returns null

*For any* active segment and any list of saved dishes where no non-deleted dish description starts with the active segment (case-insensitive ordinal), `GetSuggestion` SHALL return null.

**Validates: Requirements 1.3**

### Property 3: Exact full match returns null

*For any* saved dish in the list, if the active segment equals the dish's description in full (case-insensitive ordinal), `GetSuggestion` SHALL return null (no ghost text for already-complete names).

**Validates: Requirements 1.4**

### Property 4: Suggestion is the untyped remainder

*For any* active segment that prefix-matches a saved dish, `GetSuggestion` SHALL return exactly the substring of the matched dish description starting at position `activeSegment.Length` — i.e., only the characters the user has not yet typed.

**Validates: Requirements 2.4, 2.9**

### Property 5: Active segment extraction

*For any* input string, `ExtractActiveSegment` SHALL return the substring after the last occurrence of `" & "`. If the input contains no `" & "`, it SHALL return the entire input string unchanged.

**Validates: Requirements 4.1, 4.2**

### Property 6: Accept preserves preceding text

*For any* input text containing zero or more `" & "` delimiters and any matched dish name, `AcceptSuggestion` SHALL produce a result where everything up to and including the last `" & "` delimiter is identical to the original input, and the active segment is replaced with the matched dish name.

**Validates: Requirements 3.1, 4.3**

## Error Handling

| Scenario | Behavior |
|---|---|
| `GetSavedDishesAsync()` returns `null` dishes (cold cache, loading, error) | `_savedDishes` stays null; `GetSuggestion` returns null for any input — no ghost text shown, no errors thrown |
| `GetSavedDishesAsync()` returns empty list | Same as above — no suggestions, graceful no-op |
| JS interop `getCursorAtEnd` throws (e.g., element not yet rendered) | Catch exception, default `_cursorAtEnd` to `true` (optimistic — show ghost text) |
| Saved dish description is null or empty | `GetSuggestion` skips it (defensive null check in the filter) |
| Input exceeds 100 chars (maxlength on input) | Engine still works correctly; no special handling needed since HTML enforces the limit |
| `OnSavedDishesUpdated` fires while not editing | Update `_savedDishes` but don't compute suggestion (no-op until next input event) |

## Testing Strategy

### Property-Based Tests (FsCheck)

Library: **FsCheck 3.x** with xUnit integration.
Configuration: Minimum **100 iterations** per property.
Location: `Happie.Web.Tests/Services/DishAutocompleteEnginePropertyTests.cs`

Each property test maps directly to a design property:

| Test | Design Property | Tag |
|---|---|---|
| `GetSuggestion_Match_IsFirstSortedPrefixMatch` | Property 1 | `// Feature: dish-autocomplete, Property 1: Prefix match selects the first sorted match` |
| `GetSuggestion_NoMatch_ReturnsNull` | Property 2 | `// Feature: dish-autocomplete, Property 2: Non-matching segment returns null` |
| `GetSuggestion_ExactFullMatch_ReturnsNull` | Property 3 | `// Feature: dish-autocomplete, Property 3: Exact full match returns null` |
| `GetSuggestion_Match_ReturnsUntypedRemainder` | Property 4 | `// Feature: dish-autocomplete, Property 4: Suggestion is the untyped remainder` |
| `ExtractActiveSegment_ReturnsTextAfterLastDelimiter` | Property 5 | `// Feature: dish-autocomplete, Property 5: Active segment extraction` |
| `AcceptSuggestion_PreservesPrecedingText` | Property 6 | `// Feature: dish-autocomplete, Property 6: Accept preserves preceding text` |

### Unit Tests (xUnit)

Location: `Happie.Web.Tests/Services/DishAutocompleteEngineTests.cs`

Edge cases and specific examples:
- Empty active segment → null
- Null or empty dish list → null
- Single-character prefix matching
- Case variations (upper/lower/mixed)
- Dishes with special characters and spaces
- Delimiter at the very end of input ("Pizza & " → empty active segment → null)

### Component Tests (bUnit)

Location: `Happie.Web.Tests/Components/DishPanelAutocompleteTests.cs`

- Ghost text span rendered when suggestion exists
- Ghost text span hidden on blur
- Ghost text span hidden when cursor not at end (mocked JS interop)
- Tab key accepts suggestion and updates input value
- Right arrow at end accepts suggestion
- Right arrow not at end does not accept
- Tap on ghost text accepts suggestion
- No ghost text in saved mode
- Ghost text appears after switching from saved to custom mode

### Test Generators (FsCheck Arbitraries)

Custom generators needed:
- `ArbSavedDishList` — generates lists of `SavedDishDto` with non-empty, non-whitespace descriptions
- `ArbActiveSegment` — generates non-empty strings (1–50 chars, printable)
- `ArbInputWithDelimiters` — generates strings with 0–3 `" & "` delimiters followed by a non-empty active segment
- `ArbMatchingPair` — generates a (savedDish, prefix) pair where prefix is a proper prefix of the dish description
