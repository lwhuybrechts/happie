# Requirements Document

## Introduction

The Dish Recipes feature extends the existing SavedDish model with optional structured recipe data: a summary, an ingredients list, and cooking instructions. Housemates can view and edit recipe details on a new DishDetails page, accessible from both the SavedDishesPage and the DayPlanPage. This feature also includes renaming existing details pages to "Stats" pages and making saved dish names clickable links throughout the app.

## Glossary

- **App**: The Happie Progressive Web App (Blazor WebAssembly frontend).
- **DishDetails_Page**: The new page at route `/saved-dishes/{Id}` that displays recipe information and allows editing of a saved dish's name and recipe data.
- **DishStats_Page**: The renamed page (formerly DishDetailsPage) at route `/saved-dishes/{Id}/stats` that displays cooking statistics and timeline charts for a saved dish.
- **HousemateStats_Page**: The renamed page (formerly HousemateDetailsPage) at route `/housemates/{Id}/stats` that displays cooking statistics for a housemate.
- **SavedDishes_Page**: The existing page at route `/saved-dishes` for managing saved dishes.
- **DayPlan_Page**: The existing page at route `/day/{date}` showing the day's plan.
- **DishPanel**: The component on DayPlan_Page that displays the day's dish information.
- **Summary_Panel**: The panel on DishDetails_Page showing optional summary text, cooking duration, and serving count.
- **Ingredients_Panel**: The panel on DishDetails_Page showing the list of ingredients with checkboxes and portion scaling.
- **Instructions_Panel**: The panel on DishDetails_Page showing numbered cooking instruction paragraphs.
- **Ingredient**: A single item in the ingredients list, consisting of an amount, a unit of measurement, and a name.
- **Unit_Of_Measurement**: A value from the predefined UnitOfMeasurement enum representing a fixed list of localized units (e.g., g, kg, ml, l, tbsp, tsp, piece, stalk, clove, can, slice, pinch, handful, bunch, cup).
- **Portion_Multiplier**: A temporary, non-persisted scaling factor derived from the user-adjusted serving count divided by the base serving count.
- **Checkbox_State**: The checked/unchecked state of an ingredient, persisted per household.
- **Recipe_API**: The Azure Functions backend endpoints that handle recipe CRUD operations.
- **RecipeSummary_Table**: A dedicated Azure Table Storage table storing recipe summary metadata (summary text, cooking duration, servings) with HouseholdId as PartitionKey and SavedDishId as RowKey, separate from the SavedDish entity.
- **Household**: The group of housemates sharing the app instance.

## Requirements

### Requirement 1: Rename DishDetailsPage to DishStatsPage

**User Story:** As a developer, I want the existing DishDetailsPage renamed to DishStatsPage with an updated route, so that the `/saved-dishes/{Id}` route is freed up for the new recipe-focused DishDetails page.

#### Acceptance Criteria

1. THE App SHALL serve the former DishDetailsPage content at the route `/saved-dishes/{Id}/stats`, with the `@page` directive updated to match this route.
2. THE App SHALL no longer serve any page content at the route `/saved-dishes/{Id}` (the old stats page route is removed, freeing it for future use).
3. THE App SHALL rename the component file from `DishDetailsPage.razor` (and its associated `DishDetailsPage.razor.css`) to `DishStatsPage.razor` (and `DishStatsPage.razor.css`), and rename the corresponding test file from `DishDetailsPageTests.cs` to `DishStatsPageTests.cs`.
4. THE App SHALL update all CSS class prefixes from `dish-details-page` to `dish-stats-page` in both the `.razor` markup and the `.razor.css` stylesheet.
5. THE App SHALL update all localization keys that contain `DishDetails` in their name to use `DishStats` instead, in all `.resx` resource files and their corresponding references in code.
6. THE App SHALL update the `NavigateToStatistics` method in SavedDishesPage to navigate to `/saved-dishes/{dishId}/stats` instead of `/saved-dishes/{dishId}`.
7. THE App SHALL update all links and navigation URIs in HousemateDetailsPage that previously pointed to `/saved-dishes/{dishId}` to point to `/saved-dishes/{dishId}/stats`.

### Requirement 2: Rename HousemateDetailsPage to HousemateStatsPage

**User Story:** As a developer, I want the existing HousemateDetailsPage renamed to HousemateStatsPage with an updated route, so that naming is consistent across the app.

#### Acceptance Criteria

1. THE App SHALL serve the former HousemateDetailsPage content at the route `/housemates/{Id}/stats`, with the `@page` directive updated to match this route.
2. THE App SHALL no longer serve any page content at the route `/housemates/{Id}` (the old stats page route is removed).
3. THE App SHALL rename the component file from `HousemateDetailsPage.razor` (and its associated `HousemateDetailsPage.razor.css`) to `HousemateStatsPage.razor` (and `HousemateStatsPage.razor.css`), and rename the corresponding test file from `HousemateDetailsPageTests.cs` to `HousemateStatsPageTests.cs`.
4. THE App SHALL update all CSS class prefixes from `housemate-details-page` to `housemate-stats-page` in both the `.razor` markup and the `.razor.css` stylesheet.
5. THE App SHALL update all localization keys that contain `HousemateDetails` in their name to use `HousemateStats` instead, in all `.resx` resource files and their corresponding references in code.
6. THE App SHALL update all links and navigation URIs in DishDetailsPage (now DishStatsPage) timeline chart rows that previously pointed to `/housemates/{housemateId}` to point to `/housemates/{housemateId}/stats`.

### Requirement 3: DishDetails Page Layout and Dish Name Editing

**User Story:** As a housemate, I want a dedicated DishDetails page for each saved dish where I can view and edit the dish name, so that I have a central place to manage recipe information.

#### Acceptance Criteria

1. THE DishDetails_Page SHALL be accessible at route `/saved-dishes/{Id}`
2. THE DishDetails_Page SHALL display a back arrow button that navigates back using browser history
3. THE DishDetails_Page SHALL display the saved dish name as the page title
4. THE DishDetails_Page SHALL display an edit icon to the right of the dish name
5. WHEN the edit icon is clicked, THE DishDetails_Page SHALL replace the dish name with an editable inline text input pre-populated with the current dish name, enforcing a maximum length of 100 characters and preventing entry of the '&' character, and showing confirm and discard buttons
6. WHILE the input value is identical to the currently saved dish name, THE DishDetails_Page SHALL disable the confirm button
7. WHEN the confirm button is clicked and the input value is a non-empty string of at most 100 characters not containing '&' that differs from the currently saved name, THE DishDetails_Page SHALL save the updated dish name to the backend and display the updated name as the page title
8. WHEN the discard button is clicked, THE DishDetails_Page SHALL revert the dish name to its previous value and exit edit mode
9. IF the saved dish ID does not correspond to an existing dish, THEN THE DishDetails_Page SHALL navigate to the SavedDishes_Page
10. IF the confirm button is clicked and the input value is empty or contains only whitespace, THEN THE DishDetails_Page SHALL remain in edit mode without calling the backend
10. IF the backend returns a conflict error indicating a dish with the same name already exists, THEN THE DishDetails_Page SHALL remain in edit mode and display an error message indicating the name is already in use

### Requirement 4: Summary Panel

**User Story:** As a housemate, I want to add an optional summary, cooking duration, and serving count to a saved dish, so that I can capture basic recipe metadata.

#### Acceptance Criteria

1. THE Summary_Panel SHALL display below the dish name header on DishDetails_Page
2. THE Summary_Panel SHALL display a summary text field with a maximum length of 250 characters and placeholder text "Dish summary."
3. THE Summary_Panel SHALL display a cooking duration field in HH:MM format using an `<input type="time" step="60">` matching the dinner time input pattern on DayPlan_Page, accepting values from 00:00 to 23:59
4. THE Summary_Panel SHALL display a servings field accepting a whole number between 1 and 25
5. THE Summary_Panel SHALL display an edit icon in the panel header
6. WHEN the edit icon is clicked, THE Summary_Panel SHALL switch all fields to editable inputs and replace the edit icon with confirm and discard buttons
7. WHILE in edit mode, THE Summary_Panel SHALL disable the confirm button when no field values differ from their previously saved values
8. WHEN the confirm button is clicked, THE Summary_Panel SHALL persist all edited field values to the backend and return to read mode
9. IF the backend persistence fails, THEN THE Summary_Panel SHALL remain in edit mode, keep the user's entered values intact, and display an error message indicating the save failed
10. WHEN the discard button is clicked, THE Summary_Panel SHALL revert all fields to their previously saved values and return to read mode
11. THE Summary_Panel SHALL treat all three fields as optional, allowing each to remain empty
12. WHILE all three fields are empty and the panel is in read mode, THE Summary_Panel SHALL display placeholder text indicating no metadata has been added

### Requirement 5: Ingredients Panel Read Mode

**User Story:** As a housemate, I want to view a scaled ingredient list with checkboxes, so that I can track which ingredients I have gathered while adjusting for my desired number of servings.

#### Acceptance Criteria

1. THE Ingredients_Panel SHALL display a header showing "For X people" where X is the current adjusted serving count
2. IF the Summary_Panel serving count is empty, THEN THE Ingredients_Panel SHALL hide the minus and plus buttons and the "For X people" header
3. THE Ingredients_Panel SHALL display minus and plus buttons in the header for adjusting the portion count
4. THE Ingredients_Panel SHALL use the serving count from Summary_Panel as the base value for portion scaling
5. WHEN the plus or minus button is clicked, THE Ingredients_Panel SHALL recalculate all ingredient amounts using the ratio of adjusted serving count to base serving count, constraining the adjusted serving count to a minimum of 1 and a maximum of 25
6. THE Ingredients_Panel SHALL display each ingredient as a row containing a checkbox, a bold amount with unit, and the ingredient name
7. THE Ingredients_Panel SHALL display scaled amounts using common fractions (1/2, 1/3, 1/4, 3/4) for count-based units (piece, stalk, clove, can, slice, bunch, handful), and 2 decimal places for weight and volume units (g, kg, ml, l, tbsp, tsp, pinch, cup)
8. WHEN a checkbox is toggled, THE Ingredients_Panel SHALL immediately send a single-item PUT request to `PUT /api/saved-dishes/{id}/ingredients/{ingredientId}/check` to persist the new Checkbox_State to the backend at the household level, applying optimistic UI by toggling the checkbox immediately and rolling back on failure
9. IF persisting a Checkbox_State fails, THEN THE Ingredients_Panel SHALL revert the checkbox to its previous state and display an error message indicating the save failed
10. WHILE an ingredient checkbox is checked, THE Ingredients_Panel SHALL apply strikethrough styling to that ingredient row
11. THE Ingredients_Panel SHALL display a toggle button at the bottom labeled "Check all" when 50% or fewer ingredients are checked, and "Uncheck all" when more than 50% are checked
12. WHEN the toggle button is clicked, THE Ingredients_Panel SHALL check all ingredient checkboxes if the label is "Check all", or uncheck all ingredient checkboxes if the label is "Uncheck all", and persist each changed Checkbox_State individually via `PUT /api/saved-dishes/{id}/ingredients/{ingredientId}/check` per toggled ingredient, applying optimistic UI by toggling immediately and rolling back on failure
13. WHEN the page is revisited, THE Ingredients_Panel SHALL reset the Portion_Multiplier to the base serving count

### Requirement 6: Ingredients Panel Edit Mode

**User Story:** As a housemate, I want to add, edit, delete, and reorder ingredients, so that I can maintain an accurate ingredient list for the recipe.

#### Acceptance Criteria

1. WHILE the Ingredients_Panel is in edit mode, THE Ingredients_Panel SHALL hide all checkboxes
2. WHILE the Ingredients_Panel is in edit mode, THE Ingredients_Panel SHALL display the base serving count without plus and minus buttons
3. WHILE the Ingredients_Panel is in edit mode, THE Ingredients_Panel SHALL allow adding a new ingredient with amount (decimal number between 0.01 and 9999), unit (predefined dropdown from the UnitOfMeasurement enum including an empty "none" option), and name (text) fields
4. THE Ingredients_Panel SHALL provide a localized predefined dropdown for Unit_Of_Measurement containing exactly the values defined in the UnitOfMeasurement enum: g, kg, ml, l, tbsp, tsp, piece, stalk, clove, can, slice, pinch, handful, bunch, cup
5. WHILE the Ingredients_Panel is in edit mode, THE Ingredients_Panel SHALL allow editing the amount, unit, and name of each existing ingredient inline
6. WHILE the Ingredients_Panel is in edit mode, THE Ingredients_Panel SHALL allow deleting an ingredient, which also removes its persisted Checkbox_State
7. WHILE the Ingredients_Panel is in edit mode, THE Ingredients_Panel SHALL allow reordering ingredients via up/down arrow buttons on each row
8. WHEN the confirm button is clicked, THE Ingredients_Panel SHALL batch-save all added, edited, deleted, and reordered ingredients to the backend and exit edit mode
9. WHEN the discard button is clicked, THE Ingredients_Panel SHALL revert all ingredient changes to their previously saved values and exit edit mode
10. WHEN an ingredient is saved with an empty or whitespace-only name, THE Ingredients_Panel SHALL auto-delete that ingredient
11. IF the user attempts to add a new ingredient while 30 ingredients already exist, THEN THE Ingredients_Panel SHALL disable the add button
12. THE Ingredients_Panel SHALL enforce a maximum of 100 characters per ingredient name

### Requirement 7: Cooking Instructions Panel Read Mode

**User Story:** As a housemate, I want to view numbered cooking instructions, so that I can follow the recipe step by step.

#### Acceptance Criteria

1. THE Instructions_Panel SHALL display a panel header consistent with other panels on DishDetails_Page
2. THE Instructions_Panel SHALL display cooking instruction paragraphs as a numbered list starting at 1, ordered by their stored sort order
3. THE Instructions_Panel SHALL preserve line breaks within each numbered paragraph, rendering multiline text as entered by the user
4. IF no cooking instructions exist for the dish, THEN THE Instructions_Panel SHALL display placeholder text encouraging the user to add instructions

### Requirement 8: Cooking Instructions Panel Edit Mode

**User Story:** As a housemate, I want to add, edit, delete, and reorder cooking instruction paragraphs, so that I can maintain accurate step-by-step instructions.

#### Acceptance Criteria

1. THE Instructions_Panel SHALL display an edit icon in the panel header
2. WHEN the edit icon is clicked, THE Instructions_Panel SHALL switch to edit mode displaying a multiline textarea for each paragraph, reorder controls, delete controls, an add button below the last paragraph, and confirm and discard buttons
3. THE Instructions_Panel SHALL allow editing the text of each existing paragraph
4. THE Instructions_Panel SHALL allow deleting an instruction paragraph
5. THE Instructions_Panel SHALL allow reordering instruction paragraphs
6. WHEN paragraphs are reordered, THE Instructions_Panel SHALL auto-update numbering to maintain a continuous sequence from 1 to N
7. WHEN a paragraph is confirmed with empty or whitespace-only text, THE Instructions_Panel SHALL auto-delete that paragraph
8. THE Instructions_Panel SHALL enforce a maximum of 15 paragraphs by hiding the add button when 15 paragraphs exist
9. THE Instructions_Panel SHALL enforce a maximum of 500 characters per paragraph
10. WHEN the confirm button is clicked, THE Instructions_Panel SHALL persist all pending changes to the backend and exit edit mode
11. WHEN the discard button is clicked, THE Instructions_Panel SHALL revert all paragraphs to their previously saved state and exit edit mode

### Requirement 9: Clickable Saved Dishes on DayPlan Page

**User Story:** As a housemate, I want to tap a linked saved dish name on the DayPlan page to navigate to its DishDetails page, so that I can quickly access recipe information for tonight's dinner.

#### Acceptance Criteria

1. WHILE the DishPanel is in read mode and the `DishDto.SavedDishIds` list contains one or more IDs, THE DishPanel SHALL render each individual saved dish name as a separate clickable anchor element, with non-clickable " & " text separators between them when multiple dishes are linked
2. WHEN a saved dish name link is clicked, THE App SHALL navigate to `/saved-dishes/{dishId}` where `{dishId}` is the `Id` of the clicked saved dish
3. WHILE the DishPanel is in read mode and saved dishes are linked, THE DishPanel SHALL apply a visually distinct hover style (underline) to each clickable saved dish name on pointer hover
4. IF the DishPanel is in read mode and a saved dish ID in `SavedDishIds` does not match any dish returned by `GetSavedDishesAsync`, THEN THE DishPanel SHALL omit that unresolved dish from the rendered clickable links

### Requirement 10: Clickable Dish Rows on SavedDishesPage

**User Story:** As a housemate, I want to tap a dish name on the SavedDishesPage to navigate to its DishDetails page, so that I can easily access recipe details from the dish list.

#### Acceptance Criteria

1. THE SavedDishes_Page SHALL render each dish name as a clickable anchor element that navigates to `/saved-dishes/{dishId}` and displays a visual hover/focus indicator (e.g., underline or color change) to signal interactivity
2. THE SavedDishes_Page SHALL remove the edit icon button and the inline edit mode (edit input with accept/discard buttons) from each dish row
3. THE SavedDishes_Page SHALL retain the statistics button and delete button on each dish row, positioned to the right of the dish name

### Requirement 11: Cross-Linking Between DishDetails and DishStats Pages

**User Story:** As a housemate, I want to navigate between the recipe page and the statistics page for the same dish, so that I can quickly switch between viewing recipe details and cooking history.

#### Acceptance Criteria

1. THE DishDetails_Page SHALL display a statistics icon button in the top panel header (alongside the dish name and edit icon) that navigates to `/saved-dishes/{Id}/stats`
2. THE DishStats_Page SHALL display a recipe icon button in the header (alongside the back button and dish title) that navigates to `/saved-dishes/{Id}`
3. THE statistics icon button on DishDetails_Page SHALL use a bar chart icon consistent with the statistics icon already used on SavedDishes_Page
4. THE recipe icon button on DishStats_Page SHALL use a recognizable recipe or book icon to indicate navigation to recipe details

### Requirement 12: Recipe Backend Storage

**User Story:** As a developer, I want recipe data stored in separate Azure Table Storage tables with HouseholdId as the PartitionKey, so that recipe data is isolated per household and scales independently from dish metadata.

#### Acceptance Criteria

1. THE Recipe_API SHALL store summary (max 250 characters), cooking duration in minutes (nullable integer), and servings (nullable integer, 1 to 25) in a dedicated RecipeSummary table with PartitionKey set to HouseholdId and RowKey set to SavedDishId, separate from the SavedDish entity
2. THE Recipe_API SHALL store each ingredient as a separate row in a dedicated Ingredients table with PartitionKey set to HouseholdId and RowKey set to `{SavedDishId}_{IngredientId}`, storing Amount (double), Unit (UnitOfMeasurement enum value), Name (string, max 100 characters), and SortOrder (int), with a maximum of 30 ingredients per saved dish
3. THE Recipe_API SHALL store each cooking instruction step as a separate row in a dedicated CookingInstructions table with PartitionKey set to HouseholdId and RowKey set to `{SavedDishId}_{InstructionId}`, storing Text (string, max 500 characters) and SortOrder (int), with a maximum of 15 instructions per saved dish
4. THE Recipe_API SHALL store each ingredient's checked state as a separate row in a dedicated IngredientChecks table with PartitionKey set to HouseholdId and RowKey set to `{SavedDishId}_{IngredientId}`, storing a boolean IsChecked field, and SHALL expose individual toggle via `PUT /api/saved-dishes/{id}/ingredients/{ingredientId}/check`
5. THE Recipe_API SHALL expose separate GET endpoints per panel: `GET /api/saved-dishes/{id}/summary` returning the recipe summary data, `GET /api/saved-dishes/{id}/ingredients` returning the ingredient list and their check states, and `GET /api/saved-dishes/{id}/instructions` returning the instruction list
6. WHEN an ingredient is deleted, THE Recipe_API SHALL also delete its associated IngredientChecks row with the matching `{SavedDishId}_{IngredientId}` RowKey
7. WHEN a SavedDish is soft-deleted, THE Recipe_API SHALL NOT delete or modify its associated Ingredients, CookingInstructions, or IngredientChecks rows, leaving the recipe data intact for potential future restoration

### Requirement 13: Non-Requirements

**User Story:** As a product owner, I want to explicitly document what is out of scope, so that development does not include unsupported features.

#### Acceptance Criteria

1. THE App SHALL NOT cache recipe data (summary, ingredients, instructions, or checkbox state) in the Service Worker or IndexedDB for offline access
2. THE App SHALL NOT provide import or export functionality for recipes in any format
3. THE App SHALL NOT support image or media file uploads for recipes
4. THE App SHALL NOT support sharing, copying, or transferring recipes between households

### Requirement 14: Post-Implementation Cleanup

**User Story:** As a developer, I want the feature prompt file removed and the SavedDishesPage explanation text updated after implementation is complete, so that the specs directory stays clean and the app reflects the new functionality.

#### Acceptance Criteria

1. WHEN all other requirements in this spec have been implemented, THE developer SHALL delete the file `.kiro/specs/feature-prompts/prompt-recipes.md` from the repository
2. THE SavedDishes_Page SHALL update its explanation text so that the first sentence mentions "adding recipes" as a current capability (e.g., "Save dishes to quickly reuse them on the day plan, view their statistics, and add recipes.")
3. THE SavedDishes_Page SHALL remove "adding recipes" from the "Coming soon" sentence
