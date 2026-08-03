# Implementation Plan: Dish Recipes

## Overview

This plan implements the dish-recipes feature in incremental steps: first renaming existing pages, then building backend infrastructure (shared types, entities, repositories, handler, function), followed by frontend components (DishDetailsPage with panels), and finally wiring navigation and cleanup. Each step builds on previous work and integrates fully before moving to the next.

## Tasks

- [x] 1. Rename DishDetailsPage to DishStatsPage
  - [x] 1.1 Rename DishDetailsPage files and update route
    - Rename `DishDetailsPage.razor` to `DishStatsPage.razor` and `DishDetailsPage.razor.css` to `DishStatsPage.razor.css`
    - Update `@page` directive from `/saved-dishes/{Id}` to `/saved-dishes/{Id}/stats`
    - Update all CSS class prefixes from `dish-details-page` to `dish-stats-page` in both `.razor` and `.razor.css`
    - Update all localization keys containing `DishDetails` to use `DishStats` in `.resx` files and code references
    - Rename test file `DishDetailsPageTests.cs` to `DishStatsPageTests.cs`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 1.2 Update navigation references to DishStatsPage
    - Update `NavigateToStatistics` in SavedDishesPage to navigate to `/saved-dishes/{dishId}/stats`
    - Update all links in HousemateDetailsPage (soon HousemateStatsPage) pointing to `/saved-dishes/{dishId}` to use `/saved-dishes/{dishId}/stats`
    - _Requirements: 1.6, 1.7_

- [x] 2. Rename HousemateDetailsPage to HousemateStatsPage
  - [x] 2.1 Rename HousemateDetailsPage files and update route
    - Rename `HousemateDetailsPage.razor` to `HousemateStatsPage.razor` and `HousemateDetailsPage.razor.css` to `HousemateStatsPage.razor.css`
    - Update `@page` directive from `/housemates/{Id}` to `/housemates/{Id}/stats`
    - Update all CSS class prefixes from `housemate-details-page` to `housemate-stats-page` in both `.razor` and `.razor.css`
    - Update all localization keys containing `HousemateDetails` to use `HousemateStats` in `.resx` files and code references
    - Rename test file `HousemateDetailsPageTests.cs` to `HousemateStatsPageTests.cs`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 2.2 Update navigation references from DishStatsPage to HousemateStatsPage
    - Update timeline chart row links in DishStatsPage from `/housemates/{housemateId}` to `/housemates/{housemateId}/stats`
    - _Requirements: 2.6_

- [x] 3. Checkpoint - Verify renames
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Shared domain types and constants
  - [x] 4.1 Create UnitOfMeasurement enum and RecipeConstants
    - Create `Happie.Shared/Domain/UnitOfMeasurement.cs` with the enum (None, G, Kg, Ml, L, Tbsp, Tsp, Piece, Stalk, Clove, Can, Slice, Pinch, Handful, Bunch, Cup)
    - Create `Happie.Shared/Domain/RecipeConstants.cs` with CountBasedUnits, WeightVolumeUnits sets and all max/min constants
    - Add `DISH_ALREADY_EXISTS` to `ApiErrorCodes`
    - _Requirements: 6.4, 12.2_

  - [x] 4.2 Create shared contract types for recipe endpoints
    - Create `RecipeSummaryResponse.cs`, `IngredientsResponse.cs`, `InstructionsResponse.cs` in `Happie.Shared/Contracts/`
    - Create `IngredientDto.cs`, `CookingInstructionDto.cs`, `IngredientCheckDto.cs` in `Happie.Shared/Contracts/`
    - Create `UpdateSummaryRequest.cs`, `UpdateIngredientsRequest.cs`, `UpdateIngredientCheckRequest.cs`, `UpdateInstructionsRequest.cs` in `Happie.Shared/Contracts/`
    - _Requirements: 12.5_

- [x] 5. Backend domain models and infrastructure
  - [x] 5.1 Create server-side domain types
    - Create `Happie.Api/Domain/RecipeSummary.cs` record
    - Create `Happie.Api/Domain/Ingredient.cs` record
    - Create `Happie.Api/Domain/CookingInstruction.cs` record
    - Create `Happie.Api/Domain/IngredientCheck.cs` record
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

  - [x] 5.2 Create entity types for Azure Table Storage
    - Create `RecipeSummaryEntity.cs` in `Infrastructure/Entities/` (PartitionKey=HouseholdId, RowKey=SavedDishId)
    - Create `IngredientEntity.cs` in `Infrastructure/Entities/` (PartitionKey=HouseholdId, RowKey={SavedDishId}_{IngredientId})
    - Create `CookingInstructionEntity.cs` in `Infrastructure/Entities/` (PartitionKey=HouseholdId, RowKey={SavedDishId}_{InstructionId})
    - Create `IngredientCheckEntity.cs` in `Infrastructure/Entities/` (PartitionKey=HouseholdId, RowKey={SavedDishId}_{IngredientId})
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

  - [x] 5.3 Create mappers for new entity types
    - Create `IRecipeSummaryMapper.cs` and `RecipeSummaryMapper.cs` in `Infrastructure/Mappers/`
    - Create `IIngredientMapper.cs` and `IngredientMapper.cs` in `Infrastructure/Mappers/`
    - Create `ICookingInstructionMapper.cs` and `CookingInstructionMapper.cs` in `Infrastructure/Mappers/`
    - Create `IIngredientCheckMapper.cs` and `IngredientCheckMapper.cs` in `Infrastructure/Mappers/`
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

  - [x] 5.4 Create repository interfaces and implementations
    - Create `IRecipeSummaryRepository.cs` and `RecipeSummaryRepository.cs` in `Infrastructure/Repositories/`
    - Create `IIngredientRepository.cs` and `IngredientRepository.cs` in `Infrastructure/Repositories/`
    - Create `ICookingInstructionRepository.cs` and `CookingInstructionRepository.cs` in `Infrastructure/Repositories/`
    - Create `IIngredientCheckRepository.cs` and `IngredientCheckRepository.cs` in `Infrastructure/Repositories/`
    - Register new repositories and mappers in DI
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.6_

- [x] 6. Backend handler and function
  - [x] 6.1 Create RecipeHandler with IRecipeHandler interface
    - Create `IRecipeHandler.cs` in `Handlers/` with all method signatures (GetSummaryAsync, GetIngredientsAsync, GetInstructionsAsync, UpdateSummaryAsync, UpdateIngredientsAsync, UpdateIngredientCheckAsync, UpdateInstructionsAsync)
    - Create `RecipeHandler.cs` implementing all handler methods with validation (max ingredients, max instructions, field length constraints, servings range, ingredient cascade delete of check rows)
    - Create handler result types (`UpdateSummaryResult`, `UpdateIngredientsResult`, `UpdateIngredientCheckResult`, `UpdateInstructionsResult`) in `Results/`
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7_

  - [x] 6.2 Create RecipeFunction with all endpoints
    - Create `RecipeFunction.cs` in `Functions/` with GET and PUT endpoints for summary, ingredients, ingredient check, and instructions
    - Wire request validation, route parsing, and response mapping following existing patterns (SavedDishesFunction as reference)
    - _Requirements: 12.5_

  - [x] 6.3 Write property tests for recipe validation logic
    - **Property 1: Dish Name Validation** — verify acceptance/rejection criteria for dish names
    - **Property 2: Summary Field Validation** — verify range constraints for summary, duration, servings
    - **Property 6: Ingredient Field Validation** — verify amount range (0.01–9999) and name length (max 100)
    - **Property 10: Instruction Text Validation** — verify text acceptance (non-empty, max 500 chars)
    - **Validates: Requirements 3.5, 3.10, 4.2, 4.3, 4.4, 6.3, 6.12, 8.9**

  - [x] 6.4 Write unit tests for RecipeHandler
    - Test validation failures (exceeding max ingredients/instructions, invalid field values)
    - Test cascade delete of IngredientCheck when ingredient is removed
    - Test soft-delete isolation (recipe data preserved when dish is soft-deleted)
    - _Requirements: 12.2, 12.3, 12.6, 12.7_

  - [x] 6.5 Write unit tests for RecipeFunction
    - Test HTTP routing and response status codes
    - Test request validation and error responses
    - Test 404 for non-existent dishes
    - _Requirements: 12.5_

- [x] 7. Checkpoint - Verify backend
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Frontend: PortionScaler utility and shared logic
  - [x] 8.1 Create PortionScaler static class
    - Create `PortionScaler.cs` with `Scale` and `FormatAmount` methods (including fraction formatting for count-based units and 2 decimal places for weight/volume units)
    - Place in an appropriate shared location accessible by frontend components
    - _Requirements: 5.4, 5.5, 5.7_

  - [x] 8.2 Write property tests for PortionScaler
    - **Property 3: Portion Scaling Calculation** — verify `baseAmount * (adjustedServings / baseServings)` for all valid inputs
    - **Property 4: Amount Formatting by Unit Type** — verify fractions for count-based units, 2 decimals for weight/volume
    - **Validates: Requirements 5.4, 5.5, 5.7**

- [x] 9. Frontend: DishDetailsPage shell and dish name editing
  - [x] 9.1 Create DishDetailsPage component with route and name display
    - Create `DishDetailsPage.razor` and `DishDetailsPage.razor.css` at route `/saved-dishes/{Id}`
    - Display back arrow button using browser history navigation
    - Display dish name as page title
    - Navigate to `/saved-dishes` if dish ID doesn't exist (404 handling)
    - Add statistics icon button navigating to `/saved-dishes/{Id}/stats`
    - _Requirements: 3.1, 3.2, 3.3, 3.9, 11.1, 11.3_

  - [x] 9.2 Implement dish name inline editing
    - Display edit icon to the right of the dish name
    - On edit click: show inline text input pre-populated with current name, max 100 chars, block '&' character, show confirm/discard buttons
    - Disable confirm when input equals current saved name
    - On confirm: save to backend, display updated name; handle conflict error (409) with inline message
    - On discard: revert and exit edit mode
    - Reject empty/whitespace-only input without calling backend
    - _Requirements: 3.4, 3.5, 3.6, 3.7, 3.8, 3.10, 3.11_

  - [x] 9.3 Add localization keys for DishDetailsPage
    - Add all necessary localization keys for DishDetailsPage to `.resx` files (Dutch and English)
    - _Requirements: 3.3, 3.4, 3.5_

- [x] 10. Frontend: SummaryPanel component
  - [x] 10.1 Implement SummaryPanel read and edit modes
    - Create `SummaryPanel.razor` and `SummaryPanel.razor.css`
    - Display summary text (max 250 chars, placeholder "Dish summary."), cooking duration (HH:MM input type="time" step="60"), servings (1–25 whole number)
    - Edit icon in header; on click switch to edit mode with confirm/discard buttons
    - Disable confirm when no values changed
    - On confirm: persist to PUT /api/saved-dishes/{id}/summary, return to read mode
    - On failure: remain in edit mode, show error
    - On discard: revert all fields
    - All three fields optional; show placeholder when all empty in read mode
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11, 4.12_

- [x] 11. Frontend: IngredientsPanel component
  - [x] 11.1 Implement IngredientsPanel read mode
    - Create `IngredientsPanel.razor` and `IngredientsPanel.razor.css`
    - Display "For X people" header with minus/plus buttons (hide if servings empty)
    - Portion scaling: adjust serving count (1–25), recalculate amounts using PortionScaler
    - Display each ingredient as checkbox + bold amount with unit + name
    - Checkbox toggle: optimistic UI with PUT /api/saved-dishes/{id}/ingredients/{ingredientId}/check, rollback on failure
    - Strikethrough styling on checked ingredients
    - "Check all" / "Uncheck all" toggle button with 50% threshold logic
    - Reset portion multiplier on page visit
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10, 5.11, 5.12, 5.13_

  - [x] 11.2 Write property test for check all/uncheck all threshold
    - **Property 5: Check All/Uncheck All Label Threshold** — verify label is "Check all" when ≤50% checked, "Uncheck all" when >50% checked
    - **Validates: Requirements 5.11**

  - [x] 11.3 Implement IngredientsPanel edit mode
    - Hide checkboxes, show base serving count without +/- buttons
    - Add ingredient: amount (0.01–9999), unit (dropdown from UnitOfMeasurement enum with "none" option), name (max 100 chars)
    - Edit existing ingredients inline
    - Delete ingredient (removes check state too)
    - Reorder via up/down arrows
    - On confirm: batch PUT /api/saved-dishes/{id}/ingredients
    - On discard: revert all changes
    - Auto-delete ingredients with whitespace-only names on confirm
    - Max 30 ingredients (disable add button at limit)
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 6.10, 6.11, 6.12_

  - [x] 11.4 Write property tests for ingredient operations
    - **Property 7: Reorder Preserves Ingredients** — verify same set of ingredients after swap, only order changed
    - **Property 8: Whitespace-Only Items Auto-Delete (ingredients)** — verify whitespace-only name results in auto-delete
    - **Validates: Requirements 6.7, 6.10**

- [x] 12. Frontend: InstructionsPanel component
  - [x] 12.1 Implement InstructionsPanel read mode
    - Create `InstructionsPanel.razor` and `InstructionsPanel.razor.css`
    - Display numbered list of instruction paragraphs ordered by sort order
    - Preserve line breaks within paragraphs
    - Show placeholder when no instructions exist
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 12.2 Implement InstructionsPanel edit mode
    - Edit icon in header; switch to edit mode with textareas, reorder controls, delete controls, add button, confirm/discard
    - Allow editing paragraph text, deleting paragraphs, reordering paragraphs
    - Auto-update numbering on reorder (continuous 1 to N)
    - Auto-delete paragraphs with whitespace-only text on confirm
    - Max 15 paragraphs (hide add button at limit), max 500 chars per paragraph
    - On confirm: PUT /api/saved-dishes/{id}/instructions
    - On discard: revert all changes
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11_

  - [x] 12.3 Write property test for instruction numbering
    - **Property 9: Instruction Numbering Continuous Sequence** — verify numbering is always 1..N with no gaps after any operation
    - **Property 8: Whitespace-Only Items Auto-Delete (instructions)** — verify whitespace-only text results in auto-delete
    - **Validates: Requirements 7.2, 8.6, 8.7**

- [x] 13. Checkpoint - Verify frontend panels
  - Ensure all tests pass, ask the user if questions arise.

- [x] 14. Navigation: Clickable dish names and cross-linking
  - [x] 14.1 Make dish names clickable on DayPlanPage
    - In DishPanel read mode: render each saved dish name as a clickable anchor to `/saved-dishes/{dishId}`
    - Render non-clickable " & " separators between multiple dish names
    - Apply underline hover style
    - Omit unresolved dish IDs from rendered links
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 14.2 Write property test for unresolved dish ID omission
    - **Property 11: Unresolved Dish IDs Omitted** — verify only resolved IDs appear as links
    - **Validates: Requirements 9.4**

  - [x] 14.3 Make dish rows clickable on SavedDishesPage
    - Render each dish name as clickable anchor navigating to `/saved-dishes/{dishId}`
    - Remove inline edit icon and inline edit mode from dish rows
    - Retain statistics button and delete button on each row
    - Add visual hover/focus indicator (underline or color change)
    - _Requirements: 10.1, 10.2, 10.3_

  - [x] 14.4 Add recipe icon button to DishStatsPage header
    - Add a recipe/book icon button in DishStatsPage header navigating to `/saved-dishes/{Id}`
    - _Requirements: 11.2, 11.4_

- [x] 15. Post-implementation cleanup
  - [x] 15.1 Update SavedDishesPage explanation text and delete prompt file
    - Update explanation text first sentence to mention "adding recipes" as current capability
    - Remove "adding recipes" from the "Coming soon" sentence
    - Delete `.kiro/specs/feature-prompts/prompt-recipes.md`
    - _Requirements: 14.1, 14.2, 14.3_

- [x] 16. Final verification
  - [x] 16.1 Write property test for recipe round-trip persistence
    - **Property 12: Recipe Data Round-Trip Persistence** — verify storing and retrieving recipe data produces equivalent results
    - **Validates: Requirements 12.1, 12.2, 12.3, 12.4**

  - [x] 16.2 Write bUnit component tests for DishDetailsPage
    - Test page rendering, dish name editing flow, navigation on 404
    - Test statistics icon navigation
    - _Requirements: 3.1, 3.2, 3.3, 3.9, 11.1_

  - [x] 16.3 Write bUnit component tests for panels
    - Test SummaryPanel read/edit mode transitions and validation
    - Test IngredientsPanel checkbox toggle, portion scaling, check all/uncheck all
    - Test InstructionsPanel numbered list rendering and edit mode
    - _Requirements: 4.1–4.12, 5.1–5.13, 7.1–7.4, 8.1–8.11_

  - [x] 16.4 Write bUnit tests for clickable dish navigation
    - Test DishPanel rendering of clickable dish names with separators
    - Test SavedDishesPage clickable rows and removal of inline edit
    - _Requirements: 9.1–9.4, 10.1–10.3_

- [x] 17. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The rename tasks (1, 2) must complete before building the new DishDetailsPage to avoid route conflicts
- Backend infrastructure (tasks 4–6) must be in place before frontend panels can call APIs

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "4.1"] },
    { "id": 1, "tasks": ["1.2", "2.2", "4.2"] },
    { "id": 2, "tasks": ["5.1", "5.2"] },
    { "id": 3, "tasks": ["5.3"] },
    { "id": 4, "tasks": ["5.4"] },
    { "id": 5, "tasks": ["6.1", "8.1"] },
    { "id": 6, "tasks": ["6.2", "6.3", "8.2"] },
    { "id": 7, "tasks": ["6.4", "6.5", "9.1"] },
    { "id": 8, "tasks": ["9.2", "9.3"] },
    { "id": 9, "tasks": ["10.1"] },
    { "id": 10, "tasks": ["11.1"] },
    { "id": 11, "tasks": ["11.2", "11.3"] },
    { "id": 12, "tasks": ["11.4", "12.1"] },
    { "id": 13, "tasks": ["12.2"] },
    { "id": 14, "tasks": ["12.3", "14.1"] },
    { "id": 15, "tasks": ["14.2", "14.3", "14.4"] },
    { "id": 16, "tasks": ["15.1"] },
    { "id": 17, "tasks": ["16.1", "16.2", "16.3", "16.4"] }
  ]
}
```
